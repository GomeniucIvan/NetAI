using NetAI.Api.Application;

public class SystemStatusService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IApplicationContext _applicationContext;

    public SystemStatusService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IApplicationContext applicationContext)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _applicationContext = applicationContext;
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
            ["Repository"] = appConfig.Api?.Url
                ?? _configuration["Conversations:Repository:BaseUrl"]
                ?? "https://localhost:7247",
            ["RuntimeGateway"] = appConfig.RuntimeGateway?.Url
                ?? _configuration["Conversations:RuntimeGateway:BaseUrl"]
                ?? "https://localhost:7250",
            ["SandboxOrchestration"] = appConfig.SandboxOrchestration?.Url
                ?? _configuration["SandboxOrchestration:ApiUrl"]
                ?? "https://localhost:7251",
            ["RuntimeServer"] = appConfig.RuntimeServer?.Url ?? "https://localhost:7260"
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
