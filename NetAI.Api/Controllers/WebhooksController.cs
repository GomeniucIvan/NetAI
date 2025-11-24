using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Models.Security;
using NetAI.Api.Models.Webhooks;
using NetAI.Api.Services.Events;
using NetAI.Api.Services.Secrets;
using NetAI.Api.Services.Security;
using NetAI.Api.Services.Webhooks;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IConversationWebhookService _conversationWebhookService;
    private readonly IWebhookValidator _webhookValidator;
    private readonly IEventService _eventService;
    private readonly IEventCallbackDispatcher _callbackDispatcher;
    private readonly ISecretsService _secretsService;
    private readonly ISecurityService _securityService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IConversationWebhookService conversationWebhookService,
        IWebhookValidator webhookValidator,
        IEventService eventService,
        IEventCallbackDispatcher callbackDispatcher,
        ISecretsService secretsService,
        ISecurityService securityService,
        ILogger<WebhooksController> logger)
    {
        _conversationWebhookService = conversationWebhookService;
        _webhookValidator = webhookValidator;
        _eventService = eventService;
        _callbackDispatcher = callbackDispatcher;
        _secretsService = secretsService;
        _securityService = securityService;
        _logger = logger;
    }

    [HttpPost("{sandboxId}/conversations")]
    public async Task<IActionResult> OnConversationUpdate(
        string sandboxId,
        [FromBody] WebhookConversationDto conversation,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _conversationWebhookService
                .UpsertConversationAsync(sandboxId, sessionApiKey, conversation, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process conversation webhook for sandbox {SandboxId}", sandboxId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to process webhook." });
        }
    }

    [HttpPost("{sandboxId}/events/{conversationId:guid}")]
    public async Task<IActionResult> OnEvents(
        string sandboxId,
        Guid conversationId,
        [FromBody] List<WebhookEventDto> events,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        events ??= new List<WebhookEventDto>();

        try
        {
            SandboxInfoDto sandbox = await _webhookValidator
                .ValidateSandboxAsync(sandboxId, sessionApiKey, cancellationToken)
                .ConfigureAwait(false);

            ConversationMetadataRecord conversation = await _webhookValidator
                .EnsureConversationAsync(conversationId, sandbox, allowCreation: true, cancellationToken)
                .ConfigureAwait(false);

            await _eventService
                .SaveEventsAsync(conversation.ConversationId, events, cancellationToken)
                .ConfigureAwait(false);

            foreach (WebhookEventDto webhookEvent in events)
            {
                await _callbackDispatcher
                    .DispatchAsync(conversation.ConversationId, webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
            }

            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to ingest events for sandbox {SandboxId} conversation {ConversationId}",
                sandboxId,
                conversationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to ingest events." });
        }
    }

    [HttpGet("secrets")]
    public async Task<IActionResult> GetSecret(
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        CancellationToken cancellationToken = default)
    {
        SecurityQueryResult<AccessTokenVerificationResultDto> verification = await _securityService
            .VerifyAccessTokenAsync(accessToken ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.Success || verification.Data is null)
        {
            return StatusCode(
                verification.StatusCode,
                new { error = verification.Error ?? "Failed to verify access token." });
        }

        SecretsQueryResult<ProviderTokenInfo> providerSecret = await _secretsService
            .GetProviderTokenAsync(verification.Data.ProviderType, cancellationToken)
            .ConfigureAwait(false);

        if (!providerSecret.Success || providerSecret.Data is null)
        {
            return StatusCode(
                providerSecret.StatusCode,
                new { error = providerSecret.Error ?? "Provider secret not found." });
        }

        return Ok(new
        {
            secret = providerSecret.Data.Token ?? string.Empty,
            host = providerSecret.Data.Host
        });
    }
}
