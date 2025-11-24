using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.ConversationRuntime;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/runtime/conversations")]
public class ConversationRuntimeController : ControllerBase
{
    private readonly IConversationSessionService _conversationService;
    private readonly ILogger<ConversationRuntimeController> _logger;

    public ConversationRuntimeController(
        IConversationSessionService conversationService,
        ILogger<ConversationRuntimeController> logger)
    {
        _conversationService = conversationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ConversationResponseDto>> CreateConversation(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received runtime conversation create request.");

        try
        {
            ConversationDto conversation = await _conversationService
                .CreateConversationAsync(new CreateConversationRequestDto(), cancellationToken)
                .ConfigureAwait(false);

            ConversationResponseDto response = BuildConversationResponse(
                conversation.ConversationId,
                conversation.Status,
                conversation.RuntimeStatus,
                message: "Conversation created successfully.");

            return Ok(response);
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(ex, "Runtime unavailable when creating conversation.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (ConversationSessionException ex)
        {
            _logger.LogWarning(ex, "Failed to create runtime conversation.");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{conversationId}/start")]
    public async Task<ActionResult<ConversationResponseDto>> StartConversation(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ConversationDto conversation = await _conversationService
            .GetConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
        }

        try
        {
            ConversationResponseDto response = await _conversationService
                .StartConversationAsync(conversationId, conversation.SessionApiKey, null, cancellationToken)
                .ConfigureAwait(false);

            if (response is null)
            {
                return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
            }

            return Ok(NormalizeResponse(
                response,
                conversation.Status,
                conversation.RuntimeStatus,
                "Conversation start requested."));
        }
        catch (ConversationUnauthorizedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized runtime conversation start for {ConversationId}.", conversationId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(ex, "Runtime unavailable when starting conversation {ConversationId}.", conversationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (ConversationSessionException ex)
        {
            _logger.LogWarning(ex, "Failed to start runtime conversation {ConversationId}.", conversationId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{conversationId}/stop")]
    public async Task<ActionResult<ConversationResponseDto>> StopConversation(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ConversationDto conversation = await _conversationService
            .GetConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
        }

        try
        {
            ConversationResponseDto response = await _conversationService
                .StopConversationAsync(conversationId, conversation.SessionApiKey, cancellationToken)
                .ConfigureAwait(false);

            if (response is null)
            {
                return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
            }

            return Ok(NormalizeResponse(
                response,
                conversation.Status,
                conversation.RuntimeStatus,
                "Conversation stop requested."));
        }
        catch (ConversationUnauthorizedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized runtime conversation stop for {ConversationId}.", conversationId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(ex, "Runtime unavailable when stopping conversation {ConversationId}.", conversationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (ConversationSessionException ex)
        {
            _logger.LogWarning(ex, "Failed to stop runtime conversation {ConversationId}.", conversationId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<ConversationRuntimeInfoDto>> GetConversationInfo(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ConversationDto conversation = await _conversationService
            .GetConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
        }

        var response = new ConversationRuntimeInfoDto
        {
            ConversationId = conversation.ConversationId,
            Status = conversation.Status,
            RuntimeStatus = conversation.RuntimeStatus,
            Url = conversation.Url,
            SessionApiKey = conversation.SessionApiKey
        };

        return Ok(response);
    }

    [HttpGet("{conversationId}/config")]
    public async Task<ActionResult<RuntimeConfigResponseDto>> GetConversationConfig(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ConversationDto conversation = await _conversationService
            .GetConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
        }

        try
        {
            RuntimeConfigResponseDto config = await _conversationService
                .GetRuntimeConfigAsync(conversationId, conversation.SessionApiKey, cancellationToken)
                .ConfigureAwait(false);

            return Ok(config);
        }
        catch (ConversationUnauthorizedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized runtime config request for {ConversationId}.", conversationId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(ex, "Runtime unavailable when retrieving config for {ConversationId}.", conversationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (ConversationSessionException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve runtime config for {ConversationId}.", conversationId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("/alive")]
    public IActionResult Alive()
    {
        return Content("NetAI conversation runtime API is alive.");
    }

    private static ConversationResponseDto BuildConversationResponse(
        string conversationId,
        string conversationStatus,
        string runtimeStatus,
        string message)
    {
        return new ConversationResponseDto
        {
            Status = "ok",
            ConversationId = conversationId,
            ConversationStatus = conversationStatus,
            RuntimeStatus = runtimeStatus,
            Message = message
        };
    }

    private static ConversationResponseDto NormalizeResponse(
        ConversationResponseDto response,
        string conversationStatus,
        string runtimeStatus,
        string defaultMessage)
    {
        response.Status = string.IsNullOrWhiteSpace(response.Status) ? "ok" : response.Status;
        response.ConversationStatus ??= conversationStatus;
        response.RuntimeStatus ??= runtimeStatus;
        response.Message ??= defaultMessage;
        return response;
    }
}
