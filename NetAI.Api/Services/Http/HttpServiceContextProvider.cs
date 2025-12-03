using System;
using System.Net.Http;
using NetAI.Api.Application;

namespace NetAI.Api.Services.Http;

public interface IHttpServiceContextProvider
{
    HttpServiceContext HttpContextServer { get; }

    HttpServiceContext HttpContextOrchestration { get; }

    HttpServiceContext HttpContextRuntime { get; }

    HttpServiceContext HttpContextApi { get; }
}

public sealed class HttpServiceContextProvider : IHttpServiceContextProvider
{
    private readonly IApplicationContext _applicationContext;
    private readonly IHttpClientSelector _clientSelector;

    private HttpServiceContext _server;
    private HttpServiceContext _orchestration;
    private HttpServiceContext _runtime;
    private HttpServiceContext _api;

    public HttpServiceContextProvider(
        IApplicationContext applicationContext,
        IHttpClientSelector clientSelector)
    {
        _applicationContext = applicationContext ?? throw new ArgumentNullException(nameof(applicationContext));
        _clientSelector = clientSelector ?? throw new ArgumentNullException(nameof(clientSelector));
    }

    public HttpServiceContext HttpContextServer => _server ??= CreateContext(
        HttpClientSelector.RuntimeServerClientName,
        _applicationContext.AppConfiguration.RuntimeServer,
        () => _clientSelector.GetRuntimeServerClient());

    public HttpServiceContext HttpContextOrchestration => _orchestration ??= CreateContext(
        HttpClientSelector.SandboxOrchestrationClientName,
        _applicationContext.AppConfiguration.SandboxOrchestration,
        () => _clientSelector.GetSandboxOrchestrationClient());

    public HttpServiceContext HttpContextRuntime => _runtime ??= CreateContext(
        HttpClientSelector.RuntimeApiClientName,
        _applicationContext.AppConfiguration.RuntimeGateway,
        () => _clientSelector.GetRuntimeApiClient());

    public HttpServiceContext HttpContextApi => _api ??= CreateContext(
        HttpClientSelector.ApiClientName,
        _applicationContext.AppConfiguration.Api,
        () => _clientSelector.GetApiClient());

    private static HttpServiceContext CreateContext(
        string clientName,
        ServiceEndpoint endpoint,
        Func<HttpClient> clientFactory)
    {
        HttpClient client = clientFactory();
        string host = endpoint?.Url ?? endpoint?.Host;

        if (client.BaseAddress is null
            && !string.IsNullOrWhiteSpace(host)
            && Uri.TryCreate(host, UriKind.Absolute, out Uri baseUri))
        {
            client.BaseAddress = baseUri;
        }

        return new HttpServiceContext(clientName, host, client);
    }
}

public sealed class HttpServiceContext
{
    public HttpServiceContext(string name, string host, HttpClient client)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Host = string.IsNullOrWhiteSpace(host) ? null : host;
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public string Name { get; }

    public string Host { get; }

    public HttpClient Client { get; }
}