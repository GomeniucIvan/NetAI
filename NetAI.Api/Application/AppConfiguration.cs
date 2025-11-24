namespace NetAI.Api.Application;

public class AppConfiguration
{
    private readonly IReadOnlyDictionary<string, ServiceEndpoint> _services;

    public AppConfiguration(IReadOnlyDictionary<string, ServiceEndpoint> services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public ServiceEndpoint RuntimeServer => GetService(ServiceNames.RuntimeServer);

    public ServiceEndpoint RuntimeGateway => GetService(ServiceNames.RuntimeGateway);

    public ServiceEndpoint SandboxOrchestration => GetService(ServiceNames.SandboxOrchestration);

    public ServiceEndpoint Api => GetService(ServiceNames.Api);

    public ServiceEndpoint this[string serviceName] => GetService(serviceName);

    public IReadOnlyDictionary<string, ServiceEndpoint> Services => _services;

    private ServiceEndpoint GetService(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return null;
        }

        _services.TryGetValue(serviceName, out ServiceEndpoint endpoint);
        return endpoint;
    }

    public static class ServiceNames
    {
        public const string Api = "NetAI.Api";
        public const string RuntimeGateway = "NetAI.RuntimeGateway";
        public const string SandboxOrchestration = "NetAI.SandboxOrchestration";
        public const string RuntimeServer = "NetAI.RuntimeServer";
    }
}
