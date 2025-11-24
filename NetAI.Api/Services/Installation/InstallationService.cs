using Microsoft.Extensions.Options;
using NetAI.Api.Models.Installation;
using NetAI.Api.Data;
using Npgsql;

namespace NetAI.Api.Services.Installation;

public class InstallationService : IInstallationService
{
    private readonly IDatabaseConfigurationStore _databaseStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InstallationService> _logger;
    private readonly IOptionsMonitor<DatabaseOptions> _databaseOptionsMonitor;
    private readonly IOptionsMonitorCache<DatabaseOptions> _databaseOptionsCache;

    public InstallationService(
        IDatabaseConfigurationStore databaseStore,
        IServiceScopeFactory scopeFactory,
        ILogger<InstallationService> logger,
        IOptionsMonitor<DatabaseOptions> databaseOptionsMonitor,
        IOptionsMonitorCache<DatabaseOptions> databaseOptionsCache)
    {
        _databaseStore = databaseStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _databaseOptionsMonitor = databaseOptionsMonitor;
        _databaseOptionsCache = databaseOptionsCache;
    }

    public async Task<InstallStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        string connectionString = await _databaseStore.LoadConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return InstallStatusDto.NotConfigured("Database connection has not been configured.");
        }

        bool canConnect = await CanConnectAsync(connectionString, cancellationToken).ConfigureAwait(false);
        if (!canConnect)
        {
            return InstallStatusDto.NotConfigured("Unable to connect to the configured database. Please verify the connection settings.");
        }

        return InstallStatusDto.Configured();
    }

    public async Task<InstallationResult> InstallAsync(InstallRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return InstallationResult.Failure(StatusCodes.Status400BadRequest, "Request body is required.");
        }

        string connectionString = BuildConnectionString(request);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return InstallationResult.Failure(StatusCodes.Status400BadRequest, "A valid connection configuration is required.");
        }

        bool canConnect = await CanConnectAsync(connectionString, cancellationToken).ConfigureAwait(false);
        if (!canConnect)
        {
            return InstallationResult.Failure(StatusCodes.Status400BadRequest, "Unable to connect to the database using the provided configuration.");
        }

        try
        {
            await _databaseStore.StoreConnectionStringAsync(connectionString, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist database connection settings.");
            return InstallationResult.Failure(StatusCodes.Status500InternalServerError, "Failed to persist database connection settings.");
        }

        _databaseOptionsCache.TryRemove(Options.DefaultName);
        DatabaseOptions refreshedOptions = _databaseOptionsMonitor.Get(Options.DefaultName);
        if (string.IsNullOrWhiteSpace(refreshedOptions.ConnectionString))
        {
            _logger.LogDebug(
                "Database options cache refresh did not yield a connection string. Falling back to newly stored connection settings for subsequent initialization steps.");
        }

        try
        {
            await InitializeDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database schema after persisting connection settings.");
            return InstallationResult.Failure(StatusCodes.Status500InternalServerError, "Failed to initialize database schema.");
        }

        return InstallationResult.SuccessResult(StatusCodes.Status200OK, "Installation completed successfully.");
    }

    private async Task InitializeDatabaseAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildConnectionString(InstallRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            return request.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(request.Host)
            || string.IsNullOrWhiteSpace(request.Database)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        int port = request.Port ?? 5432;
        return $"Host={request.Host};Port={port};Database={request.Database};Username={request.Username};Password={request.Password}";
    }

    private async Task<bool> CanConnectAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await TryOpenConnectionAsync(connectionString, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            _logger.LogInformation(ex, "Database specified in the connection string does not exist. Attempting to create it.");
            bool created = await TryCreateDatabaseAsync(connectionString, cancellationToken).ConfigureAwait(false);
            if (!created)
            {
                return false;
            }

            try
            {
                await TryOpenConnectionAsync(connectionString, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception retryEx)
            {
                _logger.LogWarning(retryEx, "Failed to connect to the database after creating it using the provided connection string.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to the database using the provided connection string.");
            return false;
        }
    }

    private static async Task TryOpenConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.CloseAsync();
    }

    private async Task<bool> TryCreateDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            NpgsqlConnectionStringBuilder builder = new(connectionString);
            string targetDatabase = builder.Database;
            if (string.IsNullOrWhiteSpace(targetDatabase))
            {
                _logger.LogWarning("No database name was provided in the connection string. Unable to create database.");
                return false;
            }

            string adminDatabase = "postgres";
            if (string.Equals(targetDatabase, adminDatabase, StringComparison.OrdinalIgnoreCase))
            {
                adminDatabase = "template1";
            }

            NpgsqlConnectionStringBuilder adminBuilder = new(connectionString)
            {
                Database = adminDatabase
            };

            await using NpgsqlConnection connection = new(adminBuilder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using NpgsqlCommand command = connection.CreateCommand();
            string escapedDatabaseName = targetDatabase.Replace("\"", "\"\"");
            command.CommandText = $"CREATE DATABASE \"{escapedDatabaseName}\"";

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            _logger.LogInformation(ex, "Database already exists when attempting to create it.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create the database specified in the connection string.");
            return false;
        }
    }
}
