using NetAI.Api.Application;
using NetAI.Api.Services.Http;

public class SystemStatusService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IApplicationContext _applicationContext;
    private readonly IHttpServiceContextProvider _httpContexts;

    public SystemStatusService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IApplicationContext applicationContext,
        IHttpServiceContextProvider httpContexts)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _applicationContext = applicationContext ?? throw new ArgumentNullException(nameof(applicationContext));
        _httpContexts = httpContexts ?? throw new ArgumentNullException(nameof(httpContexts));
    }

    public record ServiceStatus(string Name, string Url, string Status);
    public record SystemStatus(DateTime Timestamp, List<ServiceStatus> Results);

    public async Task<SystemStatus> GetSystemStatusAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();

        var appConfig = _applicationContext.AppConfiguration;

        //too config

        var services = new Dictionary<string, string>
        {
            ["Repository"] = _httpContexts.HttpContextApi.Host
                             ?? appConfig.Api?.Url
                             ?? _configuration["Conversations:Repository:BaseUrl"]
                             ?? "https://localhost:7247",
            ["RuntimeGateway"] = _httpContexts.HttpContextRuntime.Host
                                 ?? appConfig.RuntimeGateway?.Url
                                 ?? _configuration["Conversations:RuntimeGateway:BaseUrl"]
                                 ?? "https://localhost:7250",
            ["SandboxOrchestration"] = _httpContexts.HttpContextOrchestration.Host
                                       ?? appConfig.SandboxOrchestration?.Url
                                       ?? _configuration["SandboxOrchestration:ApiUrl"]
                                       ?? "https://localhost:7251",
            ["RuntimeServer"] = _httpContexts.HttpContextServer.Host
                                ?? appConfig.RuntimeServer?.Url
                                ?? "https://localhost:7260"
        };

        var results = new List<ServiceStatus>();

        foreach (var (name, url) in services)
        {
            var healthUrl = url.TrimEnd('/') + "/healthz";
            string status;
            try
            {
                var resp = await client.GetAsync(healthUrl, ct);
                status = resp.IsSuccessStatusCode ? "Online" : $"Unhealthy ({(int)resp.StatusCode})";
            }
            catch (Exception ex)
            {
                status = $"Offline ({ex.Message})";
            }

            results.Add(new ServiceStatus(name, url, status));
        }

        return new SystemStatus(DateTime.UtcNow, results);
    }
}
