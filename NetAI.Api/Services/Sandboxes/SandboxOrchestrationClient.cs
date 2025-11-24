using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public class SandboxOrchestrationClient : ISandboxOrchestrationClient
{
    private const string ApiKeyHeader = "X-API-Key";
    private const string WebhookCallbackVariable = "OH_WEBHOOKS_0_BASE_URL";
    private const string AgentServerName = "AGENT_SERVER";
    private const string VscodeName = "VSCODE";

    private static readonly string[] WorkerNames = new[]
    {
        "WORKER_1",
        "WORKER_2"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly IReadOnlyDictionary<string, SandboxStatus> StatusMapping =
        new Dictionary<string, SandboxStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["running"] = SandboxStatus.RUNNING,
            ["paused"] = SandboxStatus.PAUSED,
            ["stopped"] = SandboxStatus.MISSING,
            ["starting"] = SandboxStatus.STARTING,
            ["error"] = SandboxStatus.ERROR,
        };

    private static readonly IReadOnlyDictionary<string, SandboxStatus> PodStatusMapping =
        new Dictionary<string, SandboxStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["ready"] = SandboxStatus.RUNNING,
            ["pending"] = SandboxStatus.STARTING,
            ["running"] = SandboxStatus.STARTING,
            ["failed"] = SandboxStatus.ERROR,
            ["unknown"] = SandboxStatus.ERROR,
            ["crashloopbackoff"] = SandboxStatus.ERROR,
        };

    private readonly HttpClient _httpClient;
    private readonly SandboxOrchestrationOptions _options;
    private readonly ILogger<SandboxOrchestrationClient> _logger;

    public SandboxOrchestrationClient(
        HttpClient httpClient,
        IOptions<SandboxOrchestrationOptions> options,
        ILogger<SandboxOrchestrationClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value;
        _logger = logger;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        string apiUrl = _options.ApiUrl;
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            apiUrl = Environment.GetEnvironmentVariable("SANDBOX_ORCHESTRATION_API_URL")
                ?? Environment.GetEnvironmentVariable("SANDBOX_REMOTE_RUNTIME_API_URL");

            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogDebug(
                    "Using sandbox orchestration API URL from environment variable");
                _options.ApiUrl = apiUrl;
            }
        }

        if (!string.IsNullOrWhiteSpace(apiUrl)
            && Uri.TryCreate(apiUrl, UriKind.Absolute, out Uri baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            string apiKey = Environment.GetEnvironmentVariable("SANDBOX_ORCHESTRATION_API_KEY")
                            ?? Environment.GetEnvironmentVariable("SANDBOX_API_KEY");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogDebug("Using sandbox orchestration API key from environment variable");
                _options.ApiKey = apiKey;
            }
        }
    }

    public async Task<SandboxProvisioningResult> StartSandboxAsync(
        string sandboxId,
        SandboxSpecInfoDto spec,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        ArgumentNullException.ThrowIfNull(spec);

        EnsureConfigured();

        Dictionary<string, string> environment = new(spec.InitialEnv, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_options.WebUrl))
        {
            environment[WebhookCallbackVariable] = BuildWebhookUrl(sandboxId);
        }

        var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"] = spec.Id,
            ["command"] = spec.Command?.ToArray(),
            ["working_dir"] = string.IsNullOrWhiteSpace(spec.WorkingDir) ? "/workspace" : spec.WorkingDir,
            ["environment"] = environment,
            ["session_id"] = sandboxId,
            ["resource_factor"] = _options.ResourceFactor,
            ["run_as_user"] = _options.RunAsUser,
            ["run_as_group"] = _options.RunAsGroup,
            ["fs_group"] = _options.FsGroup,
        };

        if (!string.IsNullOrWhiteSpace(_options.RuntimeClass))
        {
            payload["runtime_class"] = _options.RuntimeClass;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/start", UriKind.Relative))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, SerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        AddHeaders(request);

        using HttpResponseMessage response = await SendAsync(
                request,
                _options.StartSandboxTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new SandboxOrchestrationException($"Failed to start sandbox: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        using JsonDocument startDocument = await JsonDocument
            .ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        JsonDocument runtimeState = await TryFetchRuntimeStateAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        SandboxProvisioningResult result = BuildResult(spec, startDocument.RootElement, runtimeState?.RootElement);

        runtimeState?.Dispose();
        return result;
    }

    public async Task<SandboxProvisioningResult> ResumeSandboxAsync(
        string sandboxId,
        string runtimeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        EnsureConfigured();

        RuntimeDescriptor descriptor = await ResolveRuntimeAsync(sandboxId, runtimeId, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/resume", UriKind.Relative))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["runtime_id"] = descriptor.RuntimeId,
                }, SerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        AddHeaders(request);

        using HttpResponseMessage response = await SendAsync(request, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new SandboxOrchestrationException($"Sandbox {sandboxId} runtime was not found during resume.");
        }

        response.EnsureSuccessStatusCode();

        JsonDocument runtimeState = await TryFetchRuntimeStateAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        if (runtimeState is null)
        {
            throw new SandboxOrchestrationException($"Failed to retrieve runtime state after resuming sandbox {sandboxId}.");
        }

        SandboxProvisioningResult result = BuildResult(descriptor.Spec, runtimeState.RootElement, runtimeState.RootElement);
        runtimeState.Dispose();
        return result;
    }

    public async Task<bool> PauseSandboxAsync(
        string sandboxId,
        string runtimeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        EnsureConfigured();

        RuntimeDescriptor descriptor = await ResolveRuntimeAsync(sandboxId, runtimeId, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/pause", UriKind.Relative))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["runtime_id"] = descriptor.RuntimeId,
                }, SerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        AddHeaders(request);

        using HttpResponseMessage response = await SendAsync(request, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new SandboxOrchestrationException($"Failed to pause sandbox {sandboxId}: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        return true;
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress is null)
        {
            throw new SandboxOrchestrationException("Sandbox orchestration API URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new SandboxOrchestrationException("Sandbox orchestration API key is not configured.");
        }
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        if (!request.Headers.Contains(ApiKeyHeader))
        {
            request.Headers.Add(ApiKeyHeader, _options.ApiKey);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (timeout is null)
        {
            return await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout.Value);
        return await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
            .ConfigureAwait(false);
    }

    private string BuildWebhookUrl(string sandboxId)
    {
        string baseUrl = _options.WebUrl!.TrimEnd('/');
        return $"{baseUrl}/api/v1/webhooks/{sandboxId}";
    }

    private async Task<JsonDocument> TryFetchRuntimeStateAsync(
        string sandboxId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/sessions/{sandboxId}", UriKind.Relative));
            AddHeaders(request);

            using HttpResponseMessage response = await SendAsync(request, null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await JsonDocument
                .ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch runtime state for sandbox {SandboxId}", sandboxId);
            return null;
        }
    }

    private SandboxProvisioningResult BuildResult(
        SandboxSpecInfoDto spec,
        JsonElement startElement,
        JsonElement? runtimeElement)
    {
        string runtimeId = TryGetString(runtimeElement, "runtime_id")
                           ?? TryGetString(startElement, "runtime_id");
        string runtimeUrl = TryGetString(runtimeElement, "url")
                            ?? TryGetString(startElement, "url");
        string sessionApiKey = TryGetString(runtimeElement, "session_api_key")
                               ?? TryGetString(startElement, "session_api_key");
        string workingDir = TryGetString(runtimeElement, "working_dir")
                            ?? TryGetString(startElement, "working_dir")
                            ?? spec.WorkingDir;
        string runtimeStateJson = runtimeElement?.GetRawText() ?? startElement.GetRawText();

        SandboxStatus status = TranslateStatus(
            TryGetString(runtimeElement, "pod_status") ?? TryGetString(startElement, "pod_status"),
            TryGetString(runtimeElement, "status") ?? TryGetString(startElement, "status"));

        IReadOnlyList<ExposedUrlDto> exposedUrls = BuildExposedUrls(runtimeUrl, sessionApiKey, workingDir);
        IReadOnlyList<SandboxRuntimeHostDto> runtimeHosts = BuildRuntimeHosts(exposedUrls);

        return new SandboxProvisioningResult(
            status,
            sessionApiKey,
            runtimeId,
            runtimeUrl,
            workingDir,
            exposedUrls,
            runtimeHosts,
            runtimeStateJson);
    }

    private static IReadOnlyList<ExposedUrlDto> BuildExposedUrls(
        string runtimeUrl,
        string sessionApiKey,
        string workingDir)
    {
        if (string.IsNullOrWhiteSpace(runtimeUrl))
        {
            return Array.Empty<ExposedUrlDto>();
        }

        var urls = new List<ExposedUrlDto>
        {
            new()
            {
                Name = AgentServerName,
                Url = runtimeUrl
            }
        };

        if (!string.IsNullOrWhiteSpace(sessionApiKey) && !string.IsNullOrWhiteSpace(workingDir))
        {
            string vscodeUrl = BuildServiceUrl(runtimeUrl, "vscode");
            if (!string.IsNullOrWhiteSpace(vscodeUrl))
            {
                urls.Add(new ExposedUrlDto
                {
                    Name = VscodeName,
                    Url = $"{vscodeUrl}/?tkn={sessionApiKey}&folder={workingDir}"
                });
            }
        }

        foreach (string worker in WorkerNames)
        {
            string workerUrl = BuildServiceUrl(runtimeUrl, worker.ToLowerInvariant().Replace('_', '-'));
            if (!string.IsNullOrWhiteSpace(workerUrl))
            {
                urls.Add(new ExposedUrlDto
                {
                    Name = worker,
                    Url = workerUrl
                });
            }
        }

        return urls;
    }

    private static IReadOnlyList<SandboxRuntimeHostDto> BuildRuntimeHosts(IReadOnlyList<ExposedUrlDto> exposedUrls)
    {
        if (exposedUrls.Count == 0)
        {
            return Array.Empty<SandboxRuntimeHostDto>();
        }

        return exposedUrls
            .Select(url => new SandboxRuntimeHostDto
            {
                Name = url.Name,
                Url = url.Url,
            })
            .ToList();
    }

    private static string BuildServiceUrl(string baseUrl, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        int separatorIndex = baseUrl.IndexOf("://", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return null;
        }

        string scheme = baseUrl[..separatorIndex];
        string hostAndPath = baseUrl[(separatorIndex + 3)..];
        return $"{scheme}://{serviceName}-{hostAndPath}";
    }

    private static SandboxStatus TranslateStatus(string podStatus, string status)
    {
        if (!string.IsNullOrWhiteSpace(podStatus)
            && PodStatusMapping.TryGetValue(podStatus, out SandboxStatus mapped))
        {
            return mapped;
        }

        if (!string.IsNullOrWhiteSpace(status)
            && StatusMapping.TryGetValue(status, out mapped))
        {
            return mapped;
        }

        return SandboxStatus.MISSING;
    }

    private static string TryGetString(JsonElement? element, string propertyName)
    {
        if (element is null)
        {
            return null;
        }

        if (element.Value.ValueKind == JsonValueKind.Object
            && element.Value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private sealed record RuntimeDescriptor(string RuntimeId, SandboxSpecInfoDto Spec);

    private async Task<RuntimeDescriptor> ResolveRuntimeAsync(
        string sandboxId,
        string runtimeId,
        CancellationToken cancellationToken)
    {
        SandboxSpecInfoDto spec = new()
        {
            Id = string.Empty,
            WorkingDir = "/workspace"
        };

        JsonDocument runtimeState = await TryFetchRuntimeStateAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        try
        {
            if (runtimeState is null)
            {
                if (string.IsNullOrWhiteSpace(runtimeId))
                {
                    throw new SandboxOrchestrationException($"Runtime for sandbox {sandboxId} could not be resolved.");
                }

                return new RuntimeDescriptor(runtimeId, spec);
            }

            JsonElement root = runtimeState.RootElement;
            string resolvedRuntimeId = TryGetString(root, "runtime_id") ?? runtimeId;
            if (string.IsNullOrWhiteSpace(resolvedRuntimeId))
            {
                throw new SandboxOrchestrationException($"Runtime for sandbox {sandboxId} could not be resolved.");
            }

            string image = TryGetString(root, "image");
            string workingDir = TryGetString(root, "working_dir");
            if (!string.IsNullOrWhiteSpace(image))
            {
                spec.Id = image;
            }
            else
            {
                spec.Id = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                spec.WorkingDir = workingDir;
            }

            return new RuntimeDescriptor(resolvedRuntimeId, spec);
        }
        finally
        {
            runtimeState?.Dispose();
        }
    }
}
