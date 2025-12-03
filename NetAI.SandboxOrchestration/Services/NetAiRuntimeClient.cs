using System.Net.Http.Json;
using System.Text.Json;
using NetAI.SandboxOrchestration.Models;
using NetAI.SandboxOrchestration.Options;

namespace NetAI.SandboxOrchestration.Services;

public sealed class NetAiRuntimeClient : IOpenHandsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NetAiRuntimeClient> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public NetAiRuntimeClient(HttpClient httpClient, ILogger<NetAiRuntimeClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OpenHandsConversationResult> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/conversations", new { }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(SerializerOptions, cancellationToken);
        var conversationId = payload.TryGetProperty("conversation_id", out var idValue) ? idValue.GetString() : null;
        return new OpenHandsConversationResult
        {
            ConversationId = conversationId ?? string.Empty,
            SandboxId = conversationId ?? string.Empty,
            SessionId = conversationId ?? string.Empty,
            Status = payload.TryGetProperty("status", out var status) ? status.GetString() : "ok",
            RuntimeStatus = payload.TryGetProperty("runtime_status", out var runtime) ? runtime.GetString() : "ready",
            RuntimeUrl = payload.TryGetProperty("runtime_url", out var runtimeUrl) ? runtimeUrl.GetString() : _httpClient.BaseAddress?.ToString(),
            SessionApiKey = payload.TryGetProperty("session_api_key", out var key) ? key.GetString() : null,
            Message = payload.TryGetProperty("message", out var message) ? message.GetString() : null,
            Succeeded = true
        };
    }

    public async Task<OpenHandsConversationResult> StartConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/conversations/{conversationId}/start", new { modulePath = string.Empty }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(SerializerOptions, cancellationToken);
        return new OpenHandsConversationResult
        {
            ConversationId = conversationId,
            SandboxId = conversationId,
            SessionId = conversationId,
            Status = payload.TryGetProperty("status", out var status) ? status.GetString() : "ok",
            RuntimeStatus = payload.TryGetProperty("runtime_status", out var runtime) ? runtime.GetString() : "running",
            RuntimeUrl = payload.TryGetProperty("runtime_url", out var runtimeUrl) ? runtimeUrl.GetString() : _httpClient.BaseAddress?.ToString(),
            SessionApiKey = payload.TryGetProperty("session_api_key", out var key) ? key.GetString() : null,
            Message = payload.TryGetProperty("message", out var message) ? message.GetString() : null,
            Succeeded = true
        };
    }

    public Task<OpenHandsConversationResult> CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OpenHandsConversationResult
        {
            ConversationId = conversationId,
            SandboxId = conversationId,
            SessionId = conversationId,
            Status = "stopped",
            RuntimeStatus = "stopped",
            RuntimeUrl = _httpClient.BaseAddress?.ToString(),
            Succeeded = true
        });
    }

    public Task<OpenHandsConversationResult> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OpenHandsConversationResult
        {
            ConversationId = conversationId,
            SandboxId = conversationId,
            SessionId = conversationId,
            Status = "ok",
            RuntimeStatus = "running",
            RuntimeUrl = _httpClient.BaseAddress?.ToString(),
            Succeeded = true
        });
    }

    public Task<ServiceHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ServiceHealthResponse
        {
            IsHealthy = true,
            Status = "ok",
            Message = "NetAI runtime provider enabled"
        });
    }
}
