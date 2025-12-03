using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Services.Conversations;
using NetAI.Http;

namespace NetAI.Api.Services.Http;

public interface IHttpClientSelector : IRuntimeHttpClientProvider
{
    HttpClient GetApiClient();

    HttpClient GetExternalClient(string name, string baseUrl);

    Uri NormalizeRuntimeConversationEndpoint(string conversationUrl, string relativePath);
}


//todo change to shared http
public class HttpClientSelector : IHttpClientSelector
{
    public const string RuntimeApiClientName = "RuntimeGateway";
    public const string RuntimeServerClientName = "RuntimeServer";
    public const string SandboxOrchestrationClientName = "SandboxOrchestration";
    public const string ApiClientName = "PublicApi";

    private readonly IHttpClientFactory _clientFactory;
    private readonly RuntimeConversationGatewayOptions _runtimeOptions;
    private readonly ILogger<HttpClientSelector> _logger;

    public HttpClientSelector(
        IHttpClientFactory clientFactory,
        IOptions<RuntimeConversationGatewayOptions> runtimeOptions,
        ILogger<HttpClientSelector> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _runtimeOptions = runtimeOptions?.Value ?? new RuntimeConversationGatewayOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public HttpClient GetRuntimeApiClient()
    {
        return _clientFactory.CreateClient(RuntimeApiClientName);
    }

    public HttpClient GetRuntimeServerClient()
    {
        return _clientFactory.CreateClient(RuntimeServerClientName);
    }

    public HttpClient GetSandboxOrchestrationClient()
    {
        return _clientFactory.CreateClient(SandboxOrchestrationClientName);
    }

    public HttpClient GetApiClient()
    {
        return _clientFactory.CreateClient(ApiClientName);
    }

    public HttpClient GetExternalClient(string name, string baseUrl)
    {
        HttpClient client = _clientFactory.CreateClient(name);
        if (client.BaseAddress is null
            && !string.IsNullOrWhiteSpace(baseUrl)
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
        {
            client.BaseAddress = uri;
        }

        return client;
    }

    public Uri NormalizeRuntimeConversationEndpoint(string conversationUrl, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(conversationUrl))
        {
            throw new ArgumentException("Conversation URL is required", nameof(conversationUrl));
        }

        if (!Uri.TryCreate(conversationUrl, UriKind.Absolute, out Uri uri))
        {
            throw new ArgumentException("Conversation URL is not a valid absolute URI", nameof(conversationUrl));
        }

        if (uri.Segments.Length == 0)
        {
            throw new ArgumentException("Conversation URL is missing path information", nameof(conversationUrl));
        }

        Uri runtimeBase = GetRuntimeBaseAddress();
        if (!Uri.Compare(uri, runtimeBase, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase).Equals(0))
        {
            _logger.LogWarning(
                "Conversation URL host {ConversationHost} did not match runtime host {RuntimeHost}. Normalizing to runtime host.",
                uri.Host,
                runtimeBase.Host);

            var builder = new UriBuilder(runtimeBase)
            {
                Path = uri.AbsolutePath,
                Query = uri.Query
            };
            uri = builder.Uri;
        }

        string normalizedPath = uri.AbsolutePath.TrimEnd('/') + "/" + relativePath.TrimStart('/');
        return new UriBuilder(runtimeBase)
        {
            Path = normalizedPath,
            Query = uri.Query
        }.Uri;
    }

    private Uri GetRuntimeBaseAddress()
    {
        if (!string.IsNullOrWhiteSpace(_runtimeOptions.BaseUrl)
            && Uri.TryCreate(_runtimeOptions.BaseUrl, UriKind.Absolute, out Uri baseUri))
        {
            return baseUri;
        }

        throw new InvalidOperationException("Runtime gateway base URL is not configured.");
    }
}
