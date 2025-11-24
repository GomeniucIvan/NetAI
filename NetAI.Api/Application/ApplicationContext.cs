using System.Data;
using Microsoft.Extensions.Options;
using NetAI.Api.Data;
using Npgsql;

namespace NetAI.Api.Application;

public class ApplicationContext : IApplicationContext, IDisposable
{
    private readonly IOptionsMonitor<DatabaseOptions> _databaseOptionsMonitor;
    private readonly IDisposable _changeToken;
    private readonly ILogger<ApplicationContext> _logger;
    private readonly object _sync = new();
    private bool? _isInstalled;
    private DateTimeOffset _lastChecked = DateTimeOffset.MinValue;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    public ApplicationContext(
        IConfiguration configuration,
        IOptionsMonitor<DatabaseOptions> databaseOptionsMonitor,
        ILogger<ApplicationContext> logger)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _databaseOptionsMonitor = databaseOptionsMonitor ?? throw new ArgumentNullException(nameof(databaseOptionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        AppConfiguration = BuildAppConfiguration(configuration);
        _changeToken = _databaseOptionsMonitor.OnChange(_ => ResetInstallationState());
    }

    public AppConfiguration AppConfiguration { get; }

    public bool IsInstalled
    {
        get
        {
            bool shouldCheck;
            lock (_sync)
            {
                shouldCheck = !_isInstalled.HasValue
                               || DateTimeOffset.UtcNow - _lastChecked >= _checkInterval;
                if (!shouldCheck)
                {
                    return _isInstalled!.Value;
                }
            }

            bool installed = CheckDatabaseInstallation();

            lock (_sync)
            {
                _isInstalled = installed;
                _lastChecked = DateTimeOffset.UtcNow;
                return installed;
            }
        }
    }

    public void Dispose()
    {
        _changeToken?.Dispose();
    }

    private void ResetInstallationState()
    {
        lock (_sync)
        {
            _isInstalled = null;
            _lastChecked = DateTimeOffset.MinValue;
        }
    }

    private bool CheckDatabaseInstallation()
    {
        DatabaseOptions options = _databaseOptionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            _logger.LogDebug("No database connection string configured. Application is not installed.");
            return false;
        }

        try
        {
            using var connection = new NpgsqlConnection(options.ConnectionString);
            connection.Open();
            bool installed = connection.State == ConnectionState.Open;
            connection.Close();
            return installed;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Database connectivity check failed. Treating application as not installed.");
            return false;
        }
    }

    private static AppConfiguration BuildAppConfiguration(IConfiguration configuration)
    {
        //TODO R

        IConfigurationSection backendPorts = configuration.GetSection("BackendPorts");
        IConfigurationSection serviceUrls = configuration.GetSection("ServiceUrls");

        Dictionary<string, (string Host, int? Port, bool? UseHttps, string Url)> data =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (IConfigurationSection section in backendPorts.GetChildren())
        {
            string name = section.Key;
            string host = section["Host"];
            int? port = section.GetValue<int?>("Port");
            bool? useHttps = section.GetValue<bool?>("UseHttps");
            data[name] = (host, port, useHttps, null);
        }

        foreach (IConfigurationSection section in serviceUrls.GetChildren())
        {
            string name = section.Key;
            string url = section.Value;
            if (data.TryGetValue(name, out var existing))
            {
                data[name] = (existing.Host, existing.Port, existing.UseHttps, url);
            }
            else
            {
                data[name] = (null, null, null, url);
            }
        }

        Dictionary<string, ServiceEndpoint> services = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, var value) in data)
        {
            services[name] = new ServiceEndpoint(name, value.Host, value.Port, value.UseHttps, value.Url);
        }

        return new AppConfiguration(services);
    }
}
