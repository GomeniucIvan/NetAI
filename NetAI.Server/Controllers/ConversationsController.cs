using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NetAI.Server.Models;
using NetAI.Server.Services;

namespace NetAI.Server.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly RuntimeEventStore _store;
    private readonly WasmRuntimeHost _host;

    public ConversationsController(RuntimeEventStore store, WasmRuntimeHost host)
    {
        _store = store;
        _host = host;
    }

    [HttpPost]
    public IActionResult Create([FromQuery] string? workspace)
    {
        var state = _store.Create(workspace);
        var response = BuildConversationResponse(state, "Conversation created.");
        return Ok(response);
    }

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        var state = _store.Get(id);
        if (state is null)
        {
            return NotFound();
        }

        var response = BuildConversationResponse(state, "Conversation found.");
        return Ok(response);
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> Start(string id, [FromBody] StartRuntimeRequest? request)
    {
        var state = _store.Get(id);
        if (state is null)
        {
            return NotFound();
        }

        request ??= new StartRuntimeRequest();
        request.WorkspacePath ??= state.WorkspacePath;

        state.ConversationStatus = "running";
        state.RuntimeStatus = "running";
        _store.AppendEvent(id, "think", JsonDocument.Parse("{\"message\":\"starting\"}").RootElement);

        if (!string.IsNullOrWhiteSpace(request.ModulePath) && System.IO.File.Exists(request.ModulePath))
        {
            var result = await _host.RunAsync(request, HttpContext.RequestAborted);
            AppendRuntimeOutput(id, result);
            state.RuntimeStatus = result.ExitCode == 0 ? "completed" : "failed";
            state.ConversationStatus = state.RuntimeStatus == "completed" ? "stopped" : "error";
        }

        var response = BuildConversationResponse(state, "Conversation start requested.");
        return Ok(response);
    }

    [HttpPost("{id}/stop")]
    public IActionResult Stop(string id)
    {
        var state = _store.Get(id);
        if (state is null)
        {
            return NotFound();
        }

        state.ConversationStatus = "stopped";
        state.RuntimeStatus = "stopped";
        _store.AppendEvent(id, "finish", JsonDocument.Parse("{\"exit_code\":0}").RootElement);

        var response = BuildConversationResponse(state, "Conversation stopped.");
        return Ok(response);
    }

    [HttpGet("{id}/config")]
    public IActionResult GetConfig(string id)
    {
        var state = _store.Get(id);
        if (state is null)
        {
            return NotFound();
        }

        if (!Request.Headers.ContainsKey("Authorization") || string.IsNullOrWhiteSpace(Request.Headers.Authorization))
        {
            return Unauthorized(new { message = "Authorization header is required." });
        }

        if (!Request.Headers.TryGetValue("X-Session-API-Key", out var sessionKey) || sessionKey != state.SessionApiKey)
        {
            return Unauthorized(new { message = "Invalid session API key." });
        }

        _store.EnsureRuntimeIdentifiers(state);

        return Ok(new
        {
            status = "ok",
            conversation_status = state.ConversationStatus,
            runtime_status = state.RuntimeStatus,
            runtime_id = state.RuntimeId,
            session_id = state.SessionId,
            conversation_id = state.ConversationId,
            session_api_key = state.SessionApiKey,
            url = BuildConversationUrl(state.ConversationId),
            runtime_url = BuildRuntimeEventsUrl(state.ConversationId)
        });
    }

    [HttpGet("{id}/events")]
    public IActionResult GetEvents(string id)
    {
        var state = _store.Get(id);
        if (state is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            events = state.Events
                .OrderBy(e => e.Id)
                .Select(e => new
                {
                    id = e.Id,
                    type = e.Type,
                    payload = e.Payload,
                    created_at = e.CreatedAt
                })
        });
    }

    [HttpPost("{id}/events")]
    public IActionResult AppendEvent(string id, [FromBody] JsonElement payload)
    {
        var state = _store.Get(id);
        if (state is null)
        {
            return NotFound();
        }

        var evt = _store.AppendEvent(id, payload.GetProperty("type").GetString() ?? "observation", payload);
        return Ok(evt);
    }

    private void AppendRuntimeOutput(string id, WasmExecutionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Stdout))
        {
            _store.AppendEvent(id, "observation", JsonDocument.Parse($"{{\"stdout\":{JsonSerializer.Serialize(result.Stdout)} }}").RootElement);
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            _store.AppendEvent(id, "observation", JsonDocument.Parse($"{{\"stderr\":{JsonSerializer.Serialize(result.Stderr)} }}").RootElement);
        }

        _store.AppendEvent(id, "finish", JsonDocument.Parse($"{{\"exit_code\":{result.ExitCode}}}").RootElement);
    }

    private object BuildConversationResponse(RuntimeConversationState state, string message)
    {
        return new
        {
            status = "ok",
            conversation_status = state.ConversationStatus,
            runtime_status = state.RuntimeStatus,
            message,
            conversation_id = state.ConversationId,
            session_api_key = state.SessionApiKey,
            url = BuildConversationUrl(state.ConversationId),
            runtime_url = BuildRuntimeEventsUrl(state.ConversationId)
        };
    }

    private string BuildConversationUrl(string id)
    {
        return BuildAbsoluteUrl($"/api/conversations/{id}");
    }

    private string BuildRuntimeEventsUrl(string id)
    {
        return BuildAbsoluteUrl($"/api/conversations/{id}/events");
    }

    private string BuildAbsoluteUrl(string path)
    {
        var request = HttpContext.Request;
        var builder = new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Path = path
        };

        if (request.Host.Port.HasValue)
        {
            builder.Port = request.Host.Port.Value;
        }

        return builder.Uri.ToString();
    }
}
