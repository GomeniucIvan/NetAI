using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NetAI.SandboxOrchestration.Models;

namespace NetAI.SandboxOrchestration.Services;

public class OpenHandsClient : IOpenHandsClient
{
    private const string ServiceName = "openhands";
    private const string SessionApiKeyHeader = "X-Session-API-Key";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenHandsClient> _logger;

    public OpenHandsClient(HttpClient httpClient, ILogger<OpenHandsClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OpenHandsConversationResult> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync("api/conversations", new { }, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            ConversationResponse payload = await HandleResponseAsync<ConversationResponse>(response, "create", cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(payload.ConversationId))
            {
                throw new SandboxServiceException(ServiceName, "create", response.StatusCode, "OpenHands returned an empty conversation identifier.");
            }

            return await BuildConversationResultAsync(
                    payload.ConversationId,
                    payload.Message,
                    payload.ConversationStatus,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SandboxServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OpenHands conversation.");
            throw;
        }
    }

    public async Task<OpenHandsConversationResult> StartConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync($"api/conversations/{Uri.EscapeDataString(conversationId)}/start", new { }, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            ConversationResponse payload = await HandleResponseAsync<ConversationResponse>(response, "start", cancellationToken)
                .ConfigureAwait(false);

            return await BuildConversationResultAsync(
                    conversationId,
                    payload.Message,
                    payload.ConversationStatus,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SandboxServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start OpenHands conversation {ConversationId}.", conversationId);
            throw;
        }
    }

    public async Task<OpenHandsConversationResult> CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync($"api/conversations/{Uri.EscapeDataString(conversationId)}/stop", new { }, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            ConversationResponse payload = await HandleResponseAsync<ConversationResponse>(response, "stop", cancellationToken)
                .ConfigureAwait(false);

            return await BuildConversationResultAsync(
                    conversationId,
                    payload.Message,
                    payload.ConversationStatus,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SandboxServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop OpenHands conversation {ConversationId}.", conversationId);
            throw;
        }
    }

    public async Task<OpenHandsConversationResult> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        try
        {
            ConversationInfo info = await GetConversationInfoAsync(conversationId, cancellationToken).ConfigureAwait(false);
            if (info is null)
            {
                return null;
            }

            ConversationConfig config = await GetConversationConfigAsync(
                    conversationId,
                    info.SessionApiKey,
                    cancellationToken)
                .ConfigureAwait(false);

            return CreateConversationResult(conversationId, info, config, null, true);
        }
        catch (SandboxServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch OpenHands conversation {ConversationId}.", conversationId);
            throw;
        }
    }

    public async Task<ServiceHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync("alive", cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new ServiceHealthResponse
                {
                    IsHealthy = true,
                    Status = "healthy",
                    Message = string.IsNullOrWhiteSpace(body) ? "OpenHands server is reachable." : body
                };
            }

            OpenHandsError error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            return new ServiceHealthResponse
            {
                IsHealthy = false,
                Status = $"{ServiceName}:{(int)response.StatusCode}",
                Message = string.IsNullOrWhiteSpace(error?.Message)
                    ? response.ReasonPhrase ?? "OpenHands health check failed."
                    : error!.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenHands health check failed.");
            return new ServiceHealthResponse
            {
                IsHealthy = false,
                Status = "unreachable",
                Message = ex.Message
            };
        }
    }

    private async Task<OpenHandsConversationResult> BuildConversationResultAsync(
        string conversationId,
        string message,
        string conversationStatus,
        CancellationToken cancellationToken)
    {
        ConversationInfo info = await GetConversationInfoAsync(conversationId, cancellationToken).ConfigureAwait(false);
        ConversationConfig config = await GetConversationConfigAsync(
                conversationId,
                info?.SessionApiKey,
                cancellationToken)
            .ConfigureAwait(false);

        string status = info?.Status ?? conversationStatus ?? string.Empty;
        bool succeeded = !string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "OpenHands conversation {ConversationId}: Status={Status}, RuntimeStatus={RuntimeStatus}, Message={Message}, RuntimeUrl={RuntimeUrl}, RuntimeId={RuntimeId}, SessionId={SessionId}.",
            conversationId,
            status,
            info?.RuntimeStatus,
            message,
            info?.Url,
            config?.RuntimeId,
            config?.SessionId);

        return CreateConversationResult(conversationId, info, config, message, succeeded, status);
    }

    private static OpenHandsConversationResult CreateConversationResult(
        string conversationId,
        ConversationInfo info,
        ConversationConfig config,
        string message,
        bool succeeded,
        string statusOverride = null)
    {
        string sandboxId = config?.RuntimeId;
        string sessionId = config?.SessionId;

        return new OpenHandsConversationResult
        {
            ConversationId = conversationId,
            SandboxId = string.IsNullOrWhiteSpace(sandboxId) ? conversationId : sandboxId!,
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? conversationId : sessionId!,
            Status = statusOverride ?? info?.Status ?? string.Empty,
            RuntimeStatus = info?.RuntimeStatus,
            RuntimeUrl = info?.Url,
            SessionApiKey = info?.SessionApiKey,
            Message = message,
            Succeeded = succeeded
        };
    }

    private async Task<ConversationInfo> GetConversationInfoAsync(string conversationId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync($"api/conversations/{Uri.EscapeDataString(conversationId)}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await HandleResponseAsync<ConversationInfo>(response, "info", cancellationToken).ConfigureAwait(false);
    }

    private async Task<ConversationConfig> GetConversationConfigAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/conversations/{Uri.EscapeDataString(conversationId)}/config");

        if (!string.IsNullOrWhiteSpace(sessionApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionApiKey);
            if (!request.Headers.Contains(SessionApiKeyHeader))
            {
                request.Headers.TryAddWithoutValidation(SessionApiKeyHeader, sessionApiKey);
            }

        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await HandleResponseAsync<ConversationConfig>(response, "config", cancellationToken).ConfigureAwait(false);
    }

    private async Task<TResponse> HandleResponseAsync<TResponse>(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            TResponse payload = await response.Content.ReadFromJsonAsync<TResponse>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                throw new SandboxServiceException(ServiceName, operation, response.StatusCode, "OpenHands returned an empty response body.");
            }

            if (payload is ConversationResponse conversationResponse
                && !IsSuccessfulStatus(conversationResponse.Status))
            {
                string responseConversationId = string.IsNullOrWhiteSpace(conversationResponse.ConversationId)
                    ? "<null>"
                    : conversationResponse.ConversationId;
                string responseConversationStatus = string.IsNullOrWhiteSpace(conversationResponse.ConversationStatus)
                    ? "<null>"
                    : conversationResponse.ConversationStatus;
                string responseMessage = string.IsNullOrWhiteSpace(conversationResponse.Message)
                    ? "<none>"
                    : conversationResponse.Message!;

                _logger.LogWarning(
                    "OpenHands {Operation} response returned non-ok status. ConversationId={ConversationId}; ResponseStatus={ResponseStatus}; ConversationStatus={ConversationStatus}; Message={Message}",
                    operation,
                    responseConversationId,
                    conversationResponse.Status,
                    responseConversationStatus,
                    responseMessage);

                string returnMessage = string.IsNullOrWhiteSpace(conversationResponse.Message)
                    ? $"OpenHands returned status '{conversationResponse.Status}'."
                    : conversationResponse.Message!;

                throw new SandboxServiceException(ServiceName, operation, response.StatusCode, returnMessage, conversationResponse.Status);
            }

            return payload;
        }

        OpenHandsError error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
        string message = error?.Message;
        string errorCode = error?.Code ?? error?.MsgId;

        if (string.IsNullOrWhiteSpace(message))
        {
            message = response.ReasonPhrase ?? $"Request failed with status {(int)response.StatusCode}.";
        }

        _logger.LogWarning(
            "OpenHands {Operation} request failed with status code {StatusCode}. Message='{Message}', ErrorCode='{ErrorCode}'.",
            operation,
            response.StatusCode,
            message,
            errorCode);

        throw new SandboxServiceException(ServiceName, operation, response.StatusCode, message!, errorCode);
    }

    private static bool IsSuccessfulStatus(string status)
        => string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "running", StringComparison.OrdinalIgnoreCase);

    private static async Task<OpenHandsError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<OpenHandsError>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            string fallback = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fallback))
            {
                return null;
            }

            return new OpenHandsError
            {
                Message = fallback
            };
        }
    }

    private sealed class ConversationResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("conversation_status")]
        public string ConversationStatus { get; set; }
    }

    private sealed class ConversationInfo
    {
        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("runtime_status")]
        public string RuntimeStatus { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("session_api_key")]
        public string SessionApiKey { get; set; }
    }

    private sealed class ConversationConfig
    {
        [JsonPropertyName("runtime_id")]
        public string RuntimeId { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; }
    }

    private sealed class OpenHandsError
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("msg_id")]
        public string MsgId { get; set; }

        [JsonPropertyName("detail")]
        public string Detail { get; set; }
    }
}
