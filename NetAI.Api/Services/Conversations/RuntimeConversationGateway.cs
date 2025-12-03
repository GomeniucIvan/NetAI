using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Services.Http;
using NetAI.Extensions;

namespace NetAI.Api.Services.Conversations;

public class RuntimeConversationGateway : IRuntimeConversationGateway
{
    private const string SessionApiKeyHeader = "X-Session-API-Key";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IHttpClientSelector _httpClientSelector;
    private readonly RuntimeConversationGatewayOptions _options;
    private readonly ILogger<RuntimeConversationGateway> _logger;
    private bool _hasLoggedConfigurationWarning;

    public RuntimeConversationGateway(
        IHttpClientSelector httpClientSelector,
        IOptions<RuntimeConversationGatewayOptions> options,
        ILogger<RuntimeConversationGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientSelector = httpClientSelector ?? throw new ArgumentNullException(nameof(httpClientSelector));
        _httpClient = httpClientSelector.GetRuntimeApiClient();
        _options = options.Value;
        _logger = logger;
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (_httpClient.BaseAddress is not null)
        {
            return;
        }

        string baseUrl = _options.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = Environment.GetEnvironmentVariable("RUNTIME_GATEWAY_BASE_URL")
                ?? Environment.GetEnvironmentVariable("NETAI_RUNTIME_BASE_URL");

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogDebug("Using runtime gateway base URL from environment variable");
                _options.BaseUrl = baseUrl;
            }
        }

        if (!string.IsNullOrWhiteSpace(baseUrl)
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri baseUri))
        {
            _httpClient.BaseAddress = baseUri;
            return;
        }

        if (_hasLoggedConfigurationWarning)
        {
            return;
        }

        _hasLoggedConfigurationWarning = true;
        _logger.LogWarning("Runtime conversation gateway base URL is not configured.");
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException(
                "Runtime conversation gateway base URL is not configured.");
        }
    }

    public async Task<RuntimeConversationInitResult> InitializeConversationAsync(
        RuntimeConversationInitRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        CreateConversationRequestDto conversation = request.Conversation ?? new();
        SandboxConnectionInfoDto sandbox = request.SandboxConnection;

        string endpoint = "api/conversations";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(BuildConversationInitPayload(conversation), SerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        AddApiKeyHeader(httpRequest);

        HttpResponseMessage response = null;
        try
        {
            response = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogRuntimeGatewayConnectionFailure(
                "initialize conversation",
                endpoint,
                ex,
                sandboxRuntimeUrl: sandbox?.RuntimeUrl);
            throw new RuntimeConversationGatewayException(
                HttpStatusCode.BadGateway,
                ex.Message,
                ex.ToString());
        }

        using (response)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string message = string.IsNullOrWhiteSpace(body)
                    ? response.ReasonPhrase ?? "Runtime gateway request failed."
                    : body;
                throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new RuntimeConversationGatewayException(
                    HttpStatusCode.InternalServerError,
                    "Runtime gateway returned an empty response when creating a conversation.");
            }

            RuntimeConversationInitResult result = JsonSerializer
                .Deserialize<RuntimeConversationInitResult>(body, SerializerOptions);

            if (result is null)
            {
                throw new RuntimeConversationGatewayException(
                    HttpStatusCode.InternalServerError,
                    "Runtime gateway returned an unexpected response when creating a conversation.",
                    body);
            }

            _logger.LogDebug(
                "Runtime init for conversation {ConversationId} returned status {Status}",
                result.ConversationId,
                result.ConversationStatus);

            return result;
        }
    }

    public async Task<RuntimeConversationOperationResult> StartConversationAsync(
        RuntimeConversationOperationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        string conversationId = request.ConversationId;
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("Conversation id is required", nameof(request));
        }

        EnsureConfigured();

        string endpoint = $"api/conversations/{conversationId}/start";
        var payload = new
        {
            providers_set = request.Providers ?? Array.Empty<string>()
        };

        var fullUrl = new Uri(_httpClient.BaseAddress!, endpoint);

        Console.WriteLine($"[RuntimeConversationGateway] StartConversationAsync -> {fullUrl}");
        Console.WriteLine($"[RuntimeConversationGateway] StartConversationAsync Payload:\n{JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })}");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
        };


        AddSessionHeader(httpRequest, request.SessionApiKey);
        AddApiKeyHeader(httpRequest);

        HttpResponseMessage response = null;
        try
        {
            response = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogRuntimeGatewayConnectionFailure("start conversation", endpoint, ex, conversationId);
            throw new RuntimeConversationGatewayException(
                HttpStatusCode.BadGateway,
                ex.Message,
                ex.ToString());
        }

        using (response)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string message = string.IsNullOrWhiteSpace(body)
                    ? response.ReasonPhrase ?? "Runtime gateway request failed."
                    : body;
                throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new RuntimeConversationGatewayException(
                    HttpStatusCode.InternalServerError,
                    "Runtime gateway returned an empty response when starting a conversation.");
            }

            RuntimeConversationOperationResult result = JsonSerializer
                .Deserialize<RuntimeConversationOperationResult>(body, SerializerOptions);

            if (result is null)
            {
                throw new RuntimeConversationGatewayException(
                    HttpStatusCode.InternalServerError,
                    "Runtime gateway returned an unexpected response when starting a conversation.",
                    body);
            }

            return result;
        }
    }

    public async Task<RuntimeConversationOperationResult> StopConversationAsync(
        RuntimeConversationOperationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        string conversationId = request.ConversationId;
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("Conversation id is required", nameof(request));
        }

        EnsureConfigured();

        string endpoint = $"api/conversations/{conversationId}/stop";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        AddSessionHeader(httpRequest, request.SessionApiKey);
        AddApiKeyHeader(httpRequest);

        HttpResponseMessage response = null;
        try
        {
            response = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogRuntimeGatewayConnectionFailure(
                "stop conversation",
                endpoint,
                ex,
                conversationId);
            throw new RuntimeConversationGatewayException(
                HttpStatusCode.BadGateway,
                ex.Message,
                ex.ToString());
        }

        using (response)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string message = string.IsNullOrWhiteSpace(body)
                    ? response.ReasonPhrase ?? "Runtime gateway request failed."
                    : body;
                throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new RuntimeConversationGatewayException(
                    HttpStatusCode.InternalServerError,
                    "Runtime gateway returned an empty response when stopping a conversation.");
            }

            RuntimeConversationOperationResult result = JsonSerializer
                .Deserialize<RuntimeConversationOperationResult>(body, SerializerOptions);

            if (result is null)
            {
                throw new RuntimeConversationGatewayException(
                    HttpStatusCode.InternalServerError,
                    "Runtime gateway returned an unexpected response when stopping a conversation.",
                    body);
            }

            return result;
        }
    }

    public async Task PostEventAsync(RuntimeConversationEventRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (request.Payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Event payload is required.", nameof(request));
        }

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "events");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(request.Payload.GetRawText(), Encoding.UTF8, "application/json")
        };

        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }
    }

    public async Task PostMessageAsync(RuntimeConversationMessageRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message is required.", nameof(request));
        }

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "message");
        string payload = JsonSerializer.Serialize(new
        {
            message = request.Message,
            source = request.Source
        }, SerializerOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }
    }

    public async Task<RuntimeConversationEventsResult> GetEventsAsync(
        RuntimeConversationEventsRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, BuildEventsPath(request));
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new RuntimeConversationEventsResult();
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        List<JsonElement> events = new();
        if (document.RootElement.TryGetProperty("events", out JsonElement eventsElement)
            && eventsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement evt in eventsElement.EnumerateArray())
            {
                events.Add(evt.Clone());
            }
        }

        bool hasMore = document.RootElement.TryGetProperty("has_more", out JsonElement hasMoreElement)
            && hasMoreElement.ValueKind == JsonValueKind.True;

        return new RuntimeConversationEventsResult
        {
            Events = events,
            HasMore = hasMore
        };
    }

    public async Task<RuntimeConfigResponseDto> GetRuntimeConfigAsync(
        RuntimeConversationMetadataRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "config");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new RuntimeConfigResponseDto();
        }

        try
        {
            RuntimeConfigResponseDto parsed = JsonSerializer
                .Deserialize<RuntimeConfigResponseDto>(body, SerializerOptions);

            return parsed ?? new RuntimeConfigResponseDto();
        }
        catch (JsonException)
        {
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                "Invalid runtime configuration payload.",
                body);
        }
    }

    public async Task<VSCodeUrlResponseDto> GetVSCodeUrlAsync(
        RuntimeConversationMetadataRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "vscode-url");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new VSCodeUrlResponseDto();
        }

        try
        {
            VSCodeUrlResponseDto parsed = JsonSerializer
                .Deserialize<VSCodeUrlResponseDto>(body, SerializerOptions);

            return parsed ?? new VSCodeUrlResponseDto();
        }
        catch (JsonException)
        {
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                "Invalid VS Code URL payload.",
                body);
        }
    }

    public async Task<WebHostsResponseDto> GetWebHostsAsync(
        RuntimeConversationMetadataRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        var fullUrl = HostExtensions.BuildFullUrl(request.Host, request.ConversationUrl);

        Uri endpoint = BuildEndpoint(fullUrl, "web-hosts");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new WebHostsResponseDto();
        }

        try
        {
            WebHostsResponseDto parsed = JsonSerializer
                .Deserialize<WebHostsResponseDto>(body, SerializerOptions);

            return parsed ?? new WebHostsResponseDto();
        }
        catch (JsonException)
        {
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                "Invalid runtime hosts payload.",
                body);
        }
    }

    public async Task<RuntimeConversationMicroagentsResult> GetMicroagentsAsync(
        RuntimeConversationMicroagentsRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "microagents");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new RuntimeConversationMicroagentsResult();
        }

        try
        {
            RuntimeConversationMicroagentsResult parsed = JsonSerializer
                .Deserialize<RuntimeConversationMicroagentsResult>(body, SerializerOptions);

            if (parsed is null)
            {
                return new RuntimeConversationMicroagentsResult();
            }

            var normalized = new List<MicroagentDto>();
            foreach (MicroagentDto agent in parsed.Microagents ?? Array.Empty<MicroagentDto>())
            {
                if (agent is null)
                {
                    continue;
                }

                IReadOnlyList<InputMetadataDto> inputs = NormalizeInputs(agent.Inputs);

                normalized.Add(new MicroagentDto
                {
                    Name = agent.Name ?? string.Empty,
                    Type = agent.Type ?? string.Empty,
                    Content = agent.Content ?? string.Empty,
                    Triggers = agent.Triggers ?? Array.Empty<string>(),
                    Inputs = inputs,
                    Tools = agent.Tools ?? Array.Empty<string>()
                });
            }

            return new RuntimeConversationMicroagentsResult
            {
                Microagents = normalized
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse microagents response: {Body}",
                Truncate(body));
            return new RuntimeConversationMicroagentsResult();
        }
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(
        RuntimeConversationFileListRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        string path = string.IsNullOrWhiteSpace(request.Path)
            ? string.Empty
            : request.Path!;

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "list-files");
        string endpointUrl = string.IsNullOrEmpty(path)
            ? endpoint.ToString()
            : QueryHelpers.AddQueryString(endpoint.ToString(), "path", path);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpointUrl);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<string>();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var files = new List<string>();
                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        string value = element.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            files.Add(value);
                        }
                    }
                }

                return files;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                string error = TryExtractError(document.RootElement);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    throw new RuntimeConversationGatewayException(
                        HttpStatusCode.InternalServerError,
                        error!,
                        body);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse list-files response: {Body}", body);
        }

        return Array.Empty<string>();
    }

    public async Task<RuntimeConversationFileSelectionResult> SelectFileAsync(
        RuntimeConversationFileSelectionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.File))
        {
            return new RuntimeConversationFileSelectionResult
            {
                Error = "File path is required."
            };
        }

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "select-file");
        string endpointUrl = QueryHelpers.AddQueryString(endpoint.ToString(), "file", request.File);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpointUrl);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
        {
            string error = TryExtractError(body);
            return new RuntimeConversationFileSelectionResult
            {
                IsBinary = true,
                Error = error
            };
        }

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new RuntimeConversationFileSelectionResult
            {
                Error = "Runtime returned an empty response."
            };
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("code", out JsonElement codeElement)
                    && codeElement.ValueKind == JsonValueKind.String)
                {
                    return new RuntimeConversationFileSelectionResult
                    {
                        Code = codeElement.GetString()
                    };
                }

                string error = TryExtractError(document.RootElement);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return new RuntimeConversationFileSelectionResult
                    {
                        Error = error
                    };
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse select-file response: {Body}", body);
        }

        return new RuntimeConversationFileSelectionResult
        {
            Error = "Runtime returned an unexpected payload."
        };
    }

    public async Task<RuntimeConversationFileEditResult> ExecuteFileEditAsync(
        RuntimeConversationFileEditRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Action);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "actions/file-edit");

        var payload = new Dictionary<string, object?>
        {
            ["path"] = request.Action.Path,
            ["operation"] = NormalizeOperation(request.Action.Operation)
        };

        if (request.Action.StartLine.HasValue)
        {
            payload["start_line"] = request.Action.StartLine.Value;
        }

        if (request.Action.EndLine.HasValue)
        {
            payload["end_line"] = request.Action.EndLine.Value;
        }

        if (request.Action.Content is not null)
        {
            payload["content"] = request.Action.Content;
        }

        if (request.Action.LintEnabled.HasValue)
        {
            payload["lint_enabled"] = request.Action.LintEnabled.Value;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
        };

        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(response.StatusCode, message, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new RuntimeConversationFileEditResult
            {
                Error = "Runtime returned an empty response.",
                ErrorCode = "runtime_empty_response"
            };
        }

        try
        {
            RuntimeConversationFileEditResult? result = JsonSerializer
                .Deserialize<RuntimeConversationFileEditResult>(body, SerializerOptions);

            if (result is null)
            {
                throw new JsonException("File edit result was null");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse file-edit response: {Body}", body);
            return new RuntimeConversationFileEditResult
            {
                Error = "Unable to parse runtime response.",
                ErrorCode = "runtime_parse_error"
            };
        }
    }

    public async Task<RuntimeConversationUploadResult> UploadFilesAsync(
        RuntimeConversationUploadRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "upload-files");

        using var content = new MultipartFormDataContent();
        foreach (RuntimeConversationUploadFile file in request.Files)
        {
            var streamContent = new StreamContent(file.Content);
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            }

            content.Add(streamContent, "files", file.FileName);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };

        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        var uploaded = new List<string>();
        var skipped = new List<RuntimeUploadSkippedFile>();

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("uploaded_files", out JsonElement uploadedElement)
                    && uploadedElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in uploadedElement.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            string path = element.GetString();
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                uploaded.Add(path);
                            }
                        }
                    }
                }

                if (document.RootElement.TryGetProperty("skipped_files", out JsonElement skippedElement)
                    && skippedElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in skippedElement.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        string name = element.TryGetProperty("name", out JsonElement nameElement)
                            && nameElement.ValueKind == JsonValueKind.String
                                ? nameElement.GetString() ?? string.Empty
                                : string.Empty;

                        string reason = element.TryGetProperty("reason", out JsonElement reasonElement)
                            && reasonElement.ValueKind == JsonValueKind.String
                                ? reasonElement.GetString() ?? string.Empty
                                : string.Empty;

                        skipped.Add(new RuntimeUploadSkippedFile
                        {
                            Name = name,
                            Reason = reason
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse upload-files response: {Body}", body);
            }
        }

        return new RuntimeConversationUploadResult
        {
            UploadedFiles = uploaded,
            SkippedFiles = skipped
        };
    }

    public async Task<RuntimeZipStreamResult> ZipWorkspaceAsync(
        RuntimeConversationZipRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "zip-directory");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        await using Stream sourceStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        string fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "workspace.zip";

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            fileName = fileName.Trim('"');
        }

        string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/zip";

        return new RuntimeZipStreamResult
        {
            Content = memoryStream,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "workspace.zip" : fileName!,
            ContentType = contentType
        };
    }



    public async Task<IReadOnlyList<RuntimeGitChangeResult>> GetGitChangesAsync(
        RuntimeConversationGitChangesRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        EnsureConfigured();

        var fullUrl = HostExtensions.BuildFullUrl(request.Host, request.ConversationUrl);
        Uri endpoint = BuildEndpoint(fullUrl, "git/changes");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<RuntimeGitChangeResult>();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var results = new List<RuntimeGitChangeResult>();
                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    string path = element.TryGetProperty("path", out JsonElement pathElement)
                        && pathElement.ValueKind == JsonValueKind.String
                            ? pathElement.GetString() ?? string.Empty
                            : string.Empty;

                    string status = element.TryGetProperty("status", out JsonElement statusElement)
                        && statusElement.ValueKind == JsonValueKind.String
                            ? statusElement.GetString() ?? string.Empty
                            : string.Empty;

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    results.Add(new RuntimeGitChangeResult
                    {
                        Path = path,
                        Status = status
                    });
                }

                return results;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse git changes response: {Body}", body);
        }

        return Array.Empty<RuntimeGitChangeResult>();
    }

    public async Task<RuntimeGitDiffResult> GetGitDiffAsync(
        RuntimeConversationGitDiffRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ArgumentException("Path is required.", nameof(request));
        }

        EnsureConfigured();

        Uri endpoint = BuildEndpoint(request.ConversationUrl, "git/diff");
        string endpointUrl = QueryHelpers.AddQueryString(endpoint.ToString(), "path", request.Path);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpointUrl);
        AddSessionHeader(httpRequest, request.SessionApiKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Runtime gateway request failed."
                : body;
            throw new RuntimeConversationGatewayException(
                response.StatusCode,
                message,
                body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new RuntimeGitDiffResult();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                string original = document.RootElement.TryGetProperty("original", out JsonElement originalElement)
                                  && originalElement.ValueKind == JsonValueKind.String
                        ? originalElement.GetString()
                        : null;

                string modified = document.RootElement.TryGetProperty("modified", out JsonElement modifiedElement)
                                  && modifiedElement.ValueKind == JsonValueKind.String
                        ? modifiedElement.GetString()
                        : null;

                return new RuntimeGitDiffResult
                {
                    Original = original,
                    Modified = modified
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse git diff response: {Body}", body);
        }

        return new RuntimeGitDiffResult();
    }

    private static IReadOnlyList<InputMetadataDto> NormalizeInputs(IReadOnlyList<InputMetadataDto> inputs)
    {
        if (inputs is null || inputs.Count == 0)
        {
            return Array.Empty<InputMetadataDto>();
        }

        var normalized = new List<InputMetadataDto>(inputs.Count);
        foreach (InputMetadataDto input in inputs)
        {
            if (input is null)
            {
                normalized.Add(new InputMetadataDto());
                continue;
            }

            normalized.Add(new InputMetadataDto
            {
                Name = input.Name ?? string.Empty,
                Description = input.Description ?? string.Empty
            });
        }

        return normalized;
    }

    private static string Truncate(string value, int maxLength = 2048)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }


    private static ConversationTrigger DetermineTrigger(CreateConversationRequestDto request)
    {
        if (request.CreateMicroagent is not null)
        {
            return ConversationTrigger.MicroagentManagement;
        }

        if (request.SuggestedTask is not null)
        {
            return ConversationTrigger.SuggestedTask;
        }

        return ConversationTrigger.Gui;
    }


    private static void AddSessionHeader(HttpRequestMessage request, string sessionApiKey)
    {
        if (string.IsNullOrWhiteSpace(sessionApiKey))
        {
            return;
        }

        if (!request.Headers.Contains(SessionApiKeyHeader))
        {
            request.Headers.TryAddWithoutValidation(SessionApiKeyHeader, sessionApiKey);
        }
    }

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return;
        }

        const string ApiKeyHeaderName = "X-API-Key";
        if (!request.Headers.Contains(ApiKeyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, _options.ApiKey);
        }
    }

    private void LogRuntimeGatewayConnectionFailure(
        string operation,
        string endpoint,
        Exception exception,
        string conversationId = null,
        string sandboxRuntimeUrl = null)
    {
        string baseUrl = _httpClient.BaseAddress?.ToString() ?? "(not configured)";
        _logger.LogWarning(
            exception,
            "Failed to {Operation} via runtime gateway at {GatewayBaseUrl}{Endpoint}. ConversationId={ConversationId}; SandboxRuntimeUrl={SandboxRuntimeUrl}",
            operation,
            baseUrl,
            endpoint,
            conversationId ?? "(not specified)",
            sandboxRuntimeUrl ?? "(not specified)");
    }


    private static IDictionary<string, object> BuildConversationInitPayload(CreateConversationRequestDto conversation)
    {
        var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(conversation.Repository))
        {
            payload["repository"] = conversation.Repository;
        }

        if (!string.IsNullOrWhiteSpace(conversation.GitProvider))
        {
            payload["git_provider"] = conversation.GitProvider;
        }

        if (!string.IsNullOrWhiteSpace(conversation.SelectedBranch))
        {
            payload["selected_branch"] = conversation.SelectedBranch;
        }

        if (!string.IsNullOrWhiteSpace(conversation.InitialUserMessage))
        {
            payload["initial_user_msg"] = conversation.InitialUserMessage;
        }

        if (!string.IsNullOrWhiteSpace(conversation.ConversationInstructions))
        {
            payload["conversation_instructions"] = conversation.ConversationInstructions;
        }

        if (conversation.CreateMicroagent is not null)
        {
            var microagentPayload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(conversation.CreateMicroagent.Repo))
            {
                microagentPayload["repo"] = conversation.CreateMicroagent.Repo;
            }

            if (!string.IsNullOrWhiteSpace(conversation.CreateMicroagent.GitProvider))
            {
                microagentPayload["git_provider"] = conversation.CreateMicroagent.GitProvider;
            }

            if (!string.IsNullOrWhiteSpace(conversation.CreateMicroagent.Title))
            {
                microagentPayload["title"] = conversation.CreateMicroagent.Title;
            }

            if (microagentPayload.Count > 0)
            {
                payload["create_microagent"] = microagentPayload;
            }
        }

        return payload;
    }







    private RuntimeConversationOperationResult CreateFallbackOperationResult(
        RuntimeConversationOperationRequest request,
        ConversationStatus targetStatus,
        string logReason,
        string message = null)
    {
        string sessionApiKey = request.SessionApiKey ?? Guid.NewGuid().ToString("N");
        string runtimeId = $"runtime-{Guid.NewGuid():N}";
        string sessionId = $"session-{Guid.NewGuid():N}";
        string runtimeStatus = targetStatus == ConversationStatus.Running ? "STATUS$READY" : "STATUS$STOPPED";

        string operation = targetStatus == ConversationStatus.Running ? "start" : "stop";
        string conversationId = string.IsNullOrWhiteSpace(request.ConversationId) ? "<none>" : request.ConversationId;

        _logger.LogWarning(
            "{Reason} Returning placeholder {Operation} result for conversation {ConversationId}.",
            logReason,
            operation,
            conversationId);

        return new RuntimeConversationOperationResult
        {
            Status = "ok",
            ConversationId = request.ConversationId,
            ConversationStatus = targetStatus.ToString(),
            RuntimeStatus = runtimeStatus,
            Message = message ?? "Runtime gateway is not configured. Agent features are unavailable.",
            SessionApiKey = sessionApiKey,
            SessionId = sessionId,
            RuntimeId = runtimeId,
            Providers = request.Providers ?? Array.Empty<string>(),
            Hosts = Array.Empty<SandboxRuntimeHostDto>(),
            IsPlaceholder = true
        };
    }

    private static RuntimeConversationUploadResult CreateFallbackUploadResult(RuntimeConversationUploadRequest request)
    {
        if (request.Files.Count == 0)
        {
            return new RuntimeConversationUploadResult();
        }

        var skipped = new List<RuntimeUploadSkippedFile>();
        foreach (RuntimeConversationUploadFile file in request.Files)
        {
            skipped.Add(new RuntimeUploadSkippedFile
            {
                Name = file.FileName,
                Reason = "runtime-unavailable"
            });
        }

        return new RuntimeConversationUploadResult
        {
            UploadedFiles = Array.Empty<string>(),
            SkippedFiles = skipped
        };
    }


    private Uri BuildEndpoint(string conversationUrl, string relativePath)
    {
        return _httpClientSelector.NormalizeRuntimeConversationEndpoint(conversationUrl, relativePath);
    }

    private static string BuildEventsPath(RuntimeConversationEventsRequest request)
    {
        var parameters = new List<string>
        {
            $"start_id={Math.Max(request.StartId, 0)}",
            $"reverse={request.Reverse.ToString().ToLowerInvariant()}"
        };

        if (request.EndId.HasValue)
        {
            parameters.Add($"end_id={request.EndId.Value}");
        }

        if (request.Limit.HasValue)
        {
            int safeLimit = Math.Max(1, Math.Min(100, request.Limit.Value));
            parameters.Add($"limit={safeLimit}");
        }

        string query = string.Join("&", parameters);
        return string.IsNullOrEmpty(query) ? "events" : $"events?{query}";
    }

    private static string NormalizeOperation(RuntimeFileEditOperation operation)
    {
        return operation switch
        {
            RuntimeFileEditOperation.View => "view",
            RuntimeFileEditOperation.Insert => "insert",
            RuntimeFileEditOperation.Replace => "replace",
            RuntimeFileEditOperation.Diff => "diff",
            RuntimeFileEditOperation.ToggleLint => "toggle_lint",
            _ => "view"
        };
    }

    private static string TryExtractError(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("error", out JsonElement errorElement)
            && errorElement.ValueKind == JsonValueKind.String)
        {
            return errorElement.GetString();
        }

        return null;
    }

    private static string TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return TryExtractError(document.RootElement);
        }
        catch (JsonException)
        {
            return body;
        }
    }

}
