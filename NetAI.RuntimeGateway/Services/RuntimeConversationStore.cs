using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetAI.RuntimeGateway.Models;

namespace NetAI.RuntimeGateway.Services;

public interface IRuntimeConversationStore
{
    Task<RuntimeConversationState> CreateConversationAsync(
        string runtimeUrl = null,
        string vscodeUrl = null,
        CancellationToken cancellationToken = default);

    Task<RuntimeConversationState> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    Task<RuntimeConversationState> StartConversationAsync(
        string conversationId,
        IEnumerable<string> providers = null,
        CancellationToken cancellationToken = default);

    Task<RuntimeConversationState> StopConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    Task<RuntimeConversationEvent> AppendEventAsync(
        string conversationId,
        string type,
        JsonElement payload,
        CancellationToken cancellationToken = default);

    Task<RuntimeConversationEvent> AppendMessageAsync(
        string conversationId,
        string source,
        string message,
        CancellationToken cancellationToken = default);

    Task<RuntimeConversationEventsPage> GetEventsAsync(
        string conversationId,
        int startId,
        int? endId,
        bool reverse,
        int? limit,
        CancellationToken cancellationToken = default);
}

public sealed class RuntimeConversationStore : IRuntimeConversationStore
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RuntimeConversationStore> _logger;

    public RuntimeConversationStore(
        HttpClient httpClient,
        ILogger<RuntimeConversationStore> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _logger.LogInformation("[RuntimeGateway] RuntimeConversationStore initialized with BaseAddress={BaseAddress}", _httpClient.BaseAddress);
    }

    private string ResolveUrl(string relativePath)
    {
        var baseUri = _httpClient.BaseAddress ?? new Uri("http://localhost");
        return new Uri(baseUri, relativePath).ToString();
    }

    public async Task<RuntimeConversationState> CreateConversationAsync(
        string runtimeUrl = null,
        string vscodeUrl = null,
        CancellationToken cancellationToken = default)
    {
        _ = runtimeUrl;
        _ = vscodeUrl;

        //TODO config

        var requestUri = "conversations";

        _logger.LogInformation(
            "[RuntimeGateway] Creating runtime conversation via {RequestUri}. RuntimeUrlProvided={HasRuntimeUrl}; VscodeUrlProvided={HasVscodeUrl}",
            requestUri,
            !string.IsNullOrWhiteSpace(runtimeUrl),
            !string.IsNullOrWhiteSpace(vscodeUrl));
        Console.WriteLine($"[RuntimeGateway] POST -> {ResolveUrl(requestUri)} (create conversation)");

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            requestUri,
            new { },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        ConversationCreateResponse payload = await response.Content.ReadFromJsonAsync<ConversationCreateResponse>(
            cancellationToken: cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.ConversationId))
        {
            throw new InvalidOperationException("Conversation creation response was invalid.");
        }

        _logger.LogInformation(
            "[RuntimeGateway] Conversation created. ConversationId={ConversationId}; Status={Status}; ConversationStatus={ConversationStatus}; Message={Message}",
            payload.ConversationId,
            payload.Status ?? "<null>",
            payload.ConversationStatus ?? "<null>",
            string.IsNullOrWhiteSpace(payload.Message) ? "<none>" : payload.Message);

        RuntimeConversationState state = await GetConversationInternalAsync(
            payload.ConversationId,
            cancellationToken);

        if (state is null)
        {
            state = new RuntimeConversationState
            {
                Id = payload.ConversationId,
                ConversationStatus = payload.ConversationStatus,
                StatusMessage = payload.Message
            };
        }
        else if (!string.IsNullOrWhiteSpace(payload.Message))
        {
            state.StatusMessage = payload.Message;
        }

        return state;
    }

    public Task<RuntimeConversationState> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        return GetConversationInternalAsync(conversationId, cancellationToken);
    }

    public async Task<RuntimeConversationState> StartConversationAsync(
        string conversationId,
        IEnumerable<string> providers,
        CancellationToken cancellationToken)
    {
        var payload = new { providers_set = providers ?? Array.Empty<string>() };

        var requestUrl = $"conversations/{conversationId}/start";
        _logger.LogInformation(
            "[RuntimeGateway] Starting runtime conversation {ConversationId}. Providers={Providers}",
            conversationId,
            providers is null ? "<null>" : string.Join(',', providers));
        Console.WriteLine($"[RuntimeGateway] POST -> {ResolveUrl(requestUrl)} (start conversation)");

        using var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[RuntimeGateway] Start response ({ConversationId}) payload: {Body}", conversationId, body);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[RuntimeGateway] Failed to start runtime conversation {ConversationId}. StatusCode={StatusCode}; Body={Body}",
                conversationId,
                response.StatusCode,
                body);
            return null;
        }

        var dto = JsonSerializer.Deserialize<RuntimeConversationOperationResult>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dto == null)
        {
            _logger.LogWarning(
                "[RuntimeGateway] Unable to deserialize start response for conversation {ConversationId}. RawBody={Body}",
                conversationId,
                body);
            return null;
        }

        _logger.LogInformation(
            "[RuntimeGateway] Runtime start result for {ConversationId}: Status={Status}; ConversationStatus={ConversationStatus}; RuntimeStatus={RuntimeStatus}; Message={Message}",
            conversationId,
            dto.Status ?? "<null>",
            dto.ConversationStatus ?? "<null>",
            dto.RuntimeStatus ?? "<null>",
            string.IsNullOrWhiteSpace(dto.Message) ? "<none>" : dto.Message);

        string resolvedConversationId = dto.ConversationId ?? conversationId;
        RuntimeConversationState refreshedState = await GetConversationInternalAsync(resolvedConversationId, cancellationToken);

        if (refreshedState is null)
        {
            refreshedState = new RuntimeConversationState
            {
                Id = resolvedConversationId,
                ConversationStatus = dto.ConversationStatus,
                RuntimeStatus = dto.RuntimeStatus,
                StatusMessage = dto.Message
            };
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(dto.ConversationStatus))
            {
                refreshedState.ConversationStatus = dto.ConversationStatus;
            }

            if (!string.IsNullOrWhiteSpace(dto.RuntimeStatus))
            {
                refreshedState.RuntimeStatus = dto.RuntimeStatus;
            }

            if (!string.IsNullOrWhiteSpace(dto.Message))
            {
                refreshedState.StatusMessage = dto.Message;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.SessionApiKey))
        {
            refreshedState.SessionApiKey = dto.SessionApiKey;
        }

        if (!string.IsNullOrWhiteSpace(dto.RuntimeId))
        {
            refreshedState.RuntimeId = dto.RuntimeId;
        }

        if (!string.IsNullOrWhiteSpace(dto.SessionId))
        {
            refreshedState.SessionId = dto.SessionId;
        }

        refreshedState.Providers = providers?.ToArray() ?? Array.Empty<string>();

        return refreshedState;
    }

    public async Task<RuntimeConversationState> StopConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[RuntimeGateway] POST -> {ResolveUrl($"conversations/{conversationId}/stop")}");
        using HttpResponseMessage response = await _httpClient.PostAsync(
            $"conversations/{conversationId}/stop",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to stop conversation {ConversationId}: {StatusCode}",
                conversationId,
                response.StatusCode);
            return null;
        }

        return await GetConversationInternalAsync(conversationId, cancellationToken);
    }

    public async Task<RuntimeConversationEvent> AppendEventAsync(
        string conversationId,
        string type,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[RuntimeGateway] POST -> {ResolveUrl($"conversations/{conversationId}/events")}");
        using var content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync(
            $"conversations/{conversationId}/events",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to append event to conversation {ConversationId}: {StatusCode}",
                conversationId,
                response.StatusCode);
            return null;
        }

        return await GetLatestEventAsync(conversationId, cancellationToken);
    }

    public async Task<RuntimeConversationEvent> AppendMessageAsync(
        string conversationId,
        string source,
        string message,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            message,
            source
        };

        Console.WriteLine($"[RuntimeGateway] POST -> {ResolveUrl($"conversations/{conversationId}/message")}; length={message?.Length ?? 0}; source={source}");

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"conversations/{conversationId}/message",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to append message to conversation {ConversationId}: {StatusCode}",
                conversationId,
                response.StatusCode);
            return null;
        }

        return await GetLatestEventAsync(conversationId, cancellationToken);
    }

    public Task<RuntimeConversationEventsPage> GetEventsAsync(
        string conversationId,
        int startId,
        int? endId,
        bool reverse,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        return FetchEventsAsync(conversationId, startId, endId, reverse, limit, cancellationToken);
    }

    private async Task<RuntimeConversationState> GetConversationInternalAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[RuntimeGateway] GET -> {ResolveUrl($"conversations/{conversationId}")}");
        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"conversations/{conversationId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;

        var state = new RuntimeConversationState
        {
            Id = root.TryGetProperty("conversation_id", out JsonElement idElement)
                ? idElement.GetString() ?? conversationId
                : conversationId,
            ConversationStatus = root.TryGetProperty("status", out JsonElement statusElement)
                ? statusElement.GetString()
                : null,
            RuntimeStatus = root.TryGetProperty("runtime_status", out JsonElement runtimeStatusElement)
                ? runtimeStatusElement.GetString()
                : null,
            SessionApiKey = root.TryGetProperty("session_api_key", out JsonElement apiKeyElement)
                ? apiKeyElement.GetString()
                : null,
            RuntimeUrl = root.TryGetProperty("url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null,
            ConversationUrl = root.TryGetProperty("url", out JsonElement conversationUrlElement)
                ? conversationUrlElement.GetString()
                : null,
            CreatedAt = ParseTimestamp(root, "created_at"),
            UpdatedAt = ParseTimestamp(root, "last_updated_at")
        };

        await PopulateRuntimeConfigAsync(state, cancellationToken);
        await PopulateVscodeUrlAsync(state, cancellationToken);

        return state;
    }

    private async Task PopulateRuntimeConfigAsync(
        RuntimeConversationState state,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"conversations/{state.Id}/config",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;

        if (root.TryGetProperty("runtime_id", out JsonElement runtimeIdElement))
        {
            state.RuntimeId = runtimeIdElement.GetString();
        }

        if (root.TryGetProperty("session_id", out JsonElement sessionIdElement))
        {
            state.SessionId = sessionIdElement.GetString();
        }
    }

    private async Task PopulateVscodeUrlAsync(
        RuntimeConversationState state,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[RuntimeGateway] GET -> {ResolveUrl($"conversations/{state.Id}/vscode-url")}");
        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"conversations/{state.Id}/vscode-url",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("vscode_url", out JsonElement vscodeElement))
        {
            state.VscodeUrl = vscodeElement.GetString();
        }
    }

    private async Task<RuntimeConversationEventsPage> FetchEventsAsync(
        string conversationId,
        int startId,
        int? endId,
        bool reverse,
        int? limit,
        CancellationToken cancellationToken)
    {
        string query = BuildEventsQuery(startId, endId, reverse, limit);
        Console.WriteLine($"[RuntimeGateway] GET -> {ResolveUrl($"conversations/{conversationId}/events{query}")}");
        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"conversations/{conversationId}/events{query}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new RuntimeConversationEventsPage(Array.Empty<RuntimeConversationEvent>(), false);
        }

        response.EnsureSuccessStatusCode();

        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;
        var events = new List<RuntimeConversationEvent>();

        if (root.TryGetProperty("events", out JsonElement eventsElement)
            && eventsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in eventsElement.EnumerateArray())
            {
                RuntimeConversationEvent evt = MapEvent(item);
                if (evt is not null)
                {
                    events.Add(evt);
                }
            }
        }

        bool hasMore = root.TryGetProperty("has_more", out JsonElement hasMoreElement)
            && hasMoreElement.ValueKind == JsonValueKind.True;

        return new RuntimeConversationEventsPage(events, hasMore);
    }

    private async Task<RuntimeConversationEvent> GetLatestEventAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        RuntimeConversationEventsPage page = await FetchEventsAsync(
            conversationId,
            startId: 0,
            endId: null,
            reverse: true,
            limit: 1,
            cancellationToken: cancellationToken);

        return page.Events.FirstOrDefault();
    }

    private static RuntimeConversationEvent MapEvent(JsonElement element)
    {
        int eventId = element.TryGetProperty("id", out JsonElement idElement)
            && idElement.TryGetInt32(out int parsedId)
            ? parsedId
            : -1;

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        if (element.TryGetProperty("timestamp", out JsonElement timestampElement)
            && timestampElement.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(timestampElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsedTimestamp))
        {
            createdAt = parsedTimestamp;
        }

        string type = DetermineEventType(element);

        return new RuntimeConversationEvent
        {
            EventId = eventId,
            CreatedAt = createdAt,
            Type = type,
            PayloadJson = element.GetRawText()
        };
    }

    private static string DetermineEventType(JsonElement element)
    {
        if (element.TryGetProperty("action", out JsonElement actionElement)
            && actionElement.ValueKind == JsonValueKind.String)
        {
            return actionElement.GetString() ?? "event";
        }

        if (element.TryGetProperty("observation", out JsonElement observationElement)
            && observationElement.ValueKind == JsonValueKind.String)
        {
            return observationElement.GetString() ?? "event";
        }

        if (element.TryGetProperty("message", out JsonElement messageElement)
            && messageElement.ValueKind == JsonValueKind.String)
        {
            return "message";
        }

        if (element.TryGetProperty("type", out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString() ?? "event";
        }

        return "event";
    }

    private static DateTimeOffset? ParseTimestamp(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string BuildEventsQuery(int startId, int? endId, bool reverse, int? limit)
    {
        var parts = new List<string>
        {
            $"start_id={Math.Max(0, startId)}"
        };

        if (endId.HasValue && endId.Value >= 0)
        {
            parts.Add($"end_id={endId.Value}");
        }

        if (reverse)
        {
            parts.Add("reverse=true");
        }

        if (limit.HasValue && limit.Value > 0)
        {
            int bounded = Math.Clamp(limit.Value, 1, 100);
            parts.Add($"limit={bounded}");
        }

        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
    }

    private static string[] NormalizeProviders(IEnumerable<string> providers)
    {
        if (providers is null)
        {
            return Array.Empty<string>();
        }

        return providers
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record ConversationCreateResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; init; }

        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; }

        [JsonPropertyName("conversation_status")]
        public string ConversationStatus { get; init; }
    }

    private sealed record ProvidersSetRequest
    {
        [JsonPropertyName("providers_set")]
        public IReadOnlyList<string> ProvidersSet { get; init; }
    }
}
