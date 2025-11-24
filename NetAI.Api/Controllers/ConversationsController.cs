using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Models.Experiments;
using NetAI.Api.Services.Conversations;
using NetAI.Api.Services.Experiments;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationSessionService _conversationService;
    private readonly IExperimentConfigService _experimentConfigService;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(
        IConversationSessionService conversationService,
        IExperimentConfigService experimentConfigService,
        ILogger<ConversationsController> logger)
    {
        _conversationService = conversationService;
        _experimentConfigService = experimentConfigService;
        _logger = logger;
    }

    [HttpGet("workspace")]
    public ActionResult GetWorkspaceStatus()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet]
    public async Task<ActionResult<ResultSetDto<ConversationDto>>> GetConversations(
        [FromQuery(Name = "limit")] int limit = 20,
        [FromQuery(Name = "page_id")] string pageId = null,
        [FromQuery(Name = "selected_repository")] string selectedRepository = null,
        [FromQuery(Name = "conversation_trigger")] string conversationTrigger = null,
        CancellationToken cancellationToken = default)
    {
        var conversations = await _conversationService.GetConversationsAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken);
        return Ok(conversations);
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<ConversationDto>> GetConversation(string conversationId, CancellationToken cancellationToken)
    {
        var conversation = await _conversationService.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(conversation);
    }

    [HttpPost]
    public async Task<ActionResult<ConversationDto>> CreateConversation([FromBody] CreateConversationRequestDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received POST /api/conversations. Repository={Repository}; Branch={Branch}; GitProvider={GitProvider}; Title={Title}; Sandbox={SandboxId}",
            request?.Repository,
            request?.SelectedBranch,
            request?.GitProvider,
            request?.Title,
            request?.SandboxId);

        var conversation = await _conversationService.CreateConversationAsync(request, cancellationToken);

        _logger.LogInformation(
            "Created conversation {ConversationId} via controller with status {Status}",
            conversation.ConversationId,
            conversation.Status);

        return CreatedAtAction(nameof(GetConversation), new { conversationId = conversation.ConversationId }, conversation);
    }

    [HttpPatch("{conversationId}")]
    public async Task<ActionResult<bool>> UpdateConversation(string conversationId, [FromBody] UpdateConversationRequestDto request, CancellationToken cancellationToken)
    {
        var updated = await _conversationService.UpdateConversationAsync(conversationId, request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(true);
    }

    [HttpPost("{conversationId}/exp-config")]
    public async Task<ActionResult<bool>> AddExperimentConfig(
        string conversationId,
        [FromBody] ExperimentConfigDto experimentConfig,
        CancellationToken cancellationToken)
    {
        bool hasError = await _experimentConfigService
            .StoreExperimentConfigAsync(conversationId, experimentConfig, cancellationToken)
            .ConfigureAwait(false);
        return Ok(hasError);
    }

    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteConversation(string conversationId, CancellationToken cancellationToken)
    {
        var deleted = await _conversationService.DeleteConversationAsync(conversationId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{conversationId}/start")]
    public async Task<ActionResult<ConversationResponseDto>> StartConversation(
        string conversationId,
        [FromBody] ConversationStartRequestDto request,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received start conversation request for {ConversationId} with providers {Providers}",
            conversationId,
            request?.ProvidersSet);

        try
        {
            var conversation = await _conversationService.StartConversationAsync(conversationId, sessionApiKey, request?.ProvidersSet, cancellationToken);
            _logger.LogInformation(
                "Start conversation result for {ConversationId}: Status={Status}; RuntimeStatus={RuntimeStatus}",
                conversationId,
                conversation?.Status,
                conversation?.RuntimeStatus);
            return Ok(conversation);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost("{conversationId}/stop")]
    public async Task<ActionResult<ConversationResponseDto>> StopConversation(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = await _conversationService.StopConversationAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(conversation);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost("{conversationId}/message")]
    public async Task<ActionResult> AddMessage(
        string conversationId,
        [FromBody] ConversationMessageRequestDto request,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _conversationService.AddMessageAsync(conversationId, sessionApiKey, request, cancellationToken);
            return Ok(new { success });
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("{conversationId}/events")]
    public async Task<ActionResult<ConversationEventsPageDto>> GetEvents(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromQuery(Name = "start_id")] int startId = 0,
        [FromQuery(Name = "end_id")] int? endId = null,
        [FromQuery(Name = "reverse")] bool reverse = false,
        [FromQuery(Name = "limit")] int limit = 20,
        [FromQuery(Name = "exclude_hidden")] bool excludeHidden = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _conversationService
                .GetEventsAsync(conversationId, sessionApiKey, startId, endId, reverse, limit, excludeHidden, cancellationToken);
            return Ok(events);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost("{conversationId}/events")]
    public async Task<ActionResult> AddEvent(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _conversationService.AddEventAsync(conversationId, sessionApiKey, payload, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("{conversationId}/trajectory")]
    public async Task<ActionResult<TrajectoryResponseDto>> GetTrajectory(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trajectory = await _conversationService.GetTrajectoryAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(trajectory);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("{conversationId}/config")]
    public async Task<ActionResult<RuntimeConfigResponseDto>> GetConfig(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _conversationService.GetRuntimeConfigAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(config);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("{conversationId}/vscode-url")]
    public async Task<ActionResult<VSCodeUrlResponseDto>> GetVsCodeUrl(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _conversationService.GetVSCodeUrlAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(response);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("{conversationId}/web-hosts")]
    public async Task<ActionResult<WebHostsResponseDto>> GetWebHosts(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hosts = await _conversationService.GetWebHostsAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(hosts);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("{conversationId}/microagents")]
    public async Task<ActionResult<GetMicroagentsResponseDto>> GetMicroagents(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var microagents = await _conversationService.GetMicroagentsAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(microagents);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{conversationId}/remember-prompt")]
    public async Task<ActionResult<GetMicroagentPromptResponseDto>> GetRememberPrompt(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromQuery(Name = "event_id")] int eventId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _conversationService.GetRememberPromptAsync(conversationId, sessionApiKey, eventId, cancellationToken);
            return Ok(response);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
    }
}
