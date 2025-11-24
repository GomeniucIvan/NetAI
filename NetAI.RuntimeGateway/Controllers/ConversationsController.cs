using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.RuntimeGateway.Models;
using NetAI.RuntimeGateway.Services;

namespace NetAI.RuntimeGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IRuntimeConversationStore _store;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(IRuntimeConversationStore store, ILogger<ConversationsController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        RuntimeConversationState conversation = await _store.CreateConversationAsync(
            request?.RuntimeUrl,
            request?.VscodeUrl,
            cancellationToken);

        if (request is { InitialUserMessage: { Length: > 0 } initialMessage })
        {
            await _store.AppendMessageAsync(conversation.Id, "user", initialMessage, cancellationToken);
        }

        if (request is { ConversationInstructions: { Length: > 0 } instructions })
        {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                message = instructions,
                type = "recall",
                created_at = DateTimeOffset.UtcNow.ToString("O")
            }));

            await _store.AppendEventAsync(
                conversation.Id,
                "recall",
                document.RootElement.Clone(),
                cancellationToken);
        }

        RuntimeConversationState started = await _store.StartConversationAsync(
            conversation.Id,
            request?.ProvidersSet,
            cancellationToken);

        if (started is not null)
        {
            conversation = started;
        }

        RuntimeConversationEventsPage eventsPage = await _store.GetEventsAsync(
            conversation.Id,
            startId: 0,
            endId: null,
            reverse: false,
            limit: null,
            cancellationToken);

        return Ok(BuildConversationResponse(conversation, eventsPage.Events));
    }

    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetConversation(string conversationId, CancellationToken cancellationToken)
    {
        RuntimeConversationState conversation = await _store.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        RuntimeConversationEventsPage eventsPage = await _store.GetEventsAsync(
            conversationId,
            startId: 0,
            endId: null,
            reverse: false,
            limit: null,
            cancellationToken);

        return Ok(BuildConversationResponse(conversation, eventsPage.Events));
    }

    [HttpPost("{conversationId}/start")]
    public async Task<IActionResult> StartConversation(
        string conversationId,
        [FromBody] StartConversationRequest request,
        CancellationToken cancellationToken)
    {
        RuntimeConversationState conversation = await _store.StartConversationAsync(
            conversationId,
            request?.ProvidersSet,
            cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        RuntimeConversationEventsPage eventsPage = await _store.GetEventsAsync(
            conversationId,
            startId: 0,
            endId: null,
            reverse: false,
            limit: null,
            cancellationToken);

        return Ok(BuildConversationResponse(conversation, eventsPage.Events));
    }

    [HttpPost("{conversationId}/stop")]
    public async Task<IActionResult> StopConversation(string conversationId, CancellationToken cancellationToken)
    {
        RuntimeConversationState conversation = await _store.StopConversationAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        RuntimeConversationEventsPage eventsPage = await _store.GetEventsAsync(
            conversationId,
            startId: 0,
            endId: null,
            reverse: false,
            limit: null,
            cancellationToken);

        return Ok(BuildConversationResponse(conversation, eventsPage.Events));
    }

    [HttpPost("{conversationId}/events")]
    public async Task<IActionResult> AppendEvent(
        string conversationId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
        {
            return BadRequest(new { error = "Event payload is required." });
        }

        bool anyAppended = false;
        switch (payload.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (JsonElement item in payload.EnumerateArray())
                {
                    anyAppended |= await AppendSingleEvent(conversationId, item, cancellationToken);
                }

                break;
            default:
                anyAppended = await AppendSingleEvent(conversationId, payload, cancellationToken);
                break;
        }

        if (!anyAppended)
        {
            return NotFound();
        }

        return Accepted(new { status = "ok" });
    }

    //[HttpGet("{conversationId}/events")]
    //public async Task<IActionResult> GetEvents(
    //    string conversationId,
    //    [FromQuery(Name = "start_id")] int startId = 0,
    //    [FromQuery(Name = "end_id")] int? endId = null,
    //    [FromQuery] bool reverse = false,
    //    [FromQuery] int? limit = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    RuntimeConversationEventsPage page = await _store
    //        .GetEventsAsync(conversationId, startId, endId, reverse, limit, cancellationToken)
    //        .ConfigureAwait(false);

    //    return Ok(new
    //    {
    //        events = page.Events.Select(evt => evt.GetPayload()).ToList(),
    //        has_more = page.HasMore
    //    });
    //}

    [HttpGet("{conversationId}/config")]
    public async Task<IActionResult> GetRuntimeConfig(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        RuntimeConversationState conversation = await _store
            .GetConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            runtime_id = conversation.RuntimeId,
            session_id = conversation.SessionId
        });
    }

    [HttpGet("{conversationId}/vscode-url")]
    public async Task<IActionResult> GetVscodeUrl(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        RuntimeConversationState conversation = await _store
            .GetConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(new { vscode_url = conversation.VscodeUrl });
    }

    [HttpPost("{conversationId}/message")]
    public async Task<IActionResult> AppendMessage(
        string conversationId,
        [FromBody] MessageRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        RuntimeConversationEvent evt = await _store.AppendMessageAsync(
            conversationId,
            request.Source ?? "user",
            request.Message,
            cancellationToken);

        if (evt is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            status = "ok",
            event_id = evt.EventId
        });
    }

    [HttpGet("{conversationId}/events")]
    public async Task<IActionResult> GetEvents(
        string conversationId,
        [FromQuery(Name = "start_id")] int startId = 0,
        [FromQuery(Name = "end_id")] int? endId = null,
        [FromQuery(Name = "reverse")] bool reverse = false,
        [FromQuery(Name = "limit")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        RuntimeConversationEventsPage page = await _store.GetEventsAsync(
            conversationId,
            Math.Max(startId, 0),
            endId,
            reverse,
            limit,
            cancellationToken);

        if (page.Events.Count == 0)
        {
            RuntimeConversationState conversation = await _store.GetConversationAsync(conversationId, cancellationToken);
            if (conversation is null)
            {
                return NotFound();
            }
        }

        var payload = new
        {
            events = page.Events.Select(ToEventPayload).ToList(),
            has_more = page.HasMore
        };

        return Ok(payload);
    }

    private async Task<bool> AppendSingleEvent(
        string conversationId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        RuntimeConversationEvent evt = await _store.AppendEventAsync(
            conversationId,
            null,
            payload,
            cancellationToken);
        if (evt is null)
        {
            return false;
        }

        _logger.LogDebug(
            "Received runtime event {EventId} for conversation {ConversationId}",
            evt.EventId,
            conversationId);
        return true;
    }

    private static object BuildConversationResponse(
        RuntimeConversationState conversation,
        IReadOnlyList<RuntimeConversationEvent> events)
    {
        return new
        {
            status = "ok",
            conversation_id = conversation.Id,
            conversation_status = conversation.ConversationStatus,
            runtime_status = conversation.RuntimeStatus,
            runtime_id = conversation.RuntimeId,
            session_api_key = conversation.SessionApiKey,
            session_id = conversation.SessionId,
            runtime_url = conversation.RuntimeUrl,
            url = conversation.ConversationUrl ?? conversation.RuntimeUrl,
            vscode_url = conversation.VscodeUrl,
            message = conversation.StatusMessage,
            events = events.Select(ToEventPayload).ToList(),
            hosts = Array.Empty<object>(),
            providers = conversation.Providers
        };
    }

    private static object ToEventPayload(RuntimeConversationEvent evt)
    {
        return new
        {
            event_id = evt.EventId,
            created_at = evt.CreatedAt,
            type = evt.Type,
            payload = evt.GetPayload()
        };
    }

    public sealed class CreateConversationRequest
    {
        [JsonPropertyName("runtime_url")]
        public string RuntimeUrl { get; init; }

        [JsonPropertyName("vscode_url")]
        public string VscodeUrl { get; init; }

        [JsonPropertyName("initial_user_msg")]
        public string InitialUserMessage { get; init; }

        [JsonPropertyName("conversation_instructions")]
        public string ConversationInstructions { get; init; }

        [JsonPropertyName("providers_set")]
        public IReadOnlyList<string> ProvidersSet { get; init; }
    }

    public sealed class StartConversationRequest
    {
        [JsonPropertyName("providers_set")]
        public IReadOnlyList<string> ProvidersSet { get; init; }
    }

    public sealed class MessageRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; init; }
    }
}
