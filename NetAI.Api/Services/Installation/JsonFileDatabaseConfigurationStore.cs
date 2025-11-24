using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NetAI.Api.Services.Installation;

public class JsonFileDatabaseConfigurationStore : IDatabaseConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath;
    private readonly ILogger<JsonFileDatabaseConfigurationStore> _logger;

    public JsonFileDatabaseConfigurationStore(IHostEnvironment hostEnvironment, ILogger<JsonFileDatabaseConfigurationStore> logger)
    {
        _logger = logger;
        string baseDirectory = hostEnvironment.ContentRootPath;
        _filePath = Path.Combine(baseDirectory, "Data", "settings", "connection.json");
    }

    public async Task<string> LoadConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        bool lockAcquired = false;
        try
        {
            await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockAcquired = true;

            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                await using FileStream readStream = File.OpenRead(_filePath);
                JsonDocument document = await JsonDocument.ParseAsync(readStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (TryReadConnectionString(document.RootElement, out string connectionString))
                {
                    return connectionString;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read database configuration from {FilePath}", _filePath);
            }

            return null;
        }
        finally
        {
            if (lockAcquired)
            {
                _mutex.Release();
            }
        }
    }

    public async Task StoreConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
        }

        bool lockAcquired = false;
        try
        {
            await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockAcquired = true;

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            DatabaseConfigurationPayload payload = new()
            {
                ConnectionStrings = new ConnectionStringsSection
                {
                    Default = connectionString
                }
            };

            string json = JsonSerializer.Serialize(payload, SerializerOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist database configuration to {FilePath}", _filePath);
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                _mutex.Release();
            }
        }
    }

    private static bool TryReadConnectionString(JsonElement rootElement, out string connectionString)
    {
        connectionString = null;

        if (rootElement.ValueKind == JsonValueKind.String)
        {
            connectionString = rootElement.GetString();
            return !string.IsNullOrWhiteSpace(connectionString);
        }

        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (rootElement.TryGetProperty("connectionString", out JsonElement lowerCaseConnectionString)
            && !string.IsNullOrWhiteSpace(lowerCaseConnectionString.GetString()))
        {
            connectionString = lowerCaseConnectionString.GetString();
            return true;
        }

        if (rootElement.TryGetProperty("ConnectionString", out JsonElement connectionStringProperty)
            && !string.IsNullOrWhiteSpace(connectionStringProperty.GetString()))
        {
            connectionString = connectionStringProperty.GetString();
            return true;
        }

        if (rootElement.TryGetProperty("ConnectionStrings", out JsonElement connectionStrings)
            && connectionStrings.ValueKind == JsonValueKind.Object
            && connectionStrings.TryGetProperty("Default", out JsonElement defaultConnection)
            && !string.IsNullOrWhiteSpace(defaultConnection.GetString()))
        {
            connectionString = defaultConnection.GetString();
            return true;
        }

        return false;
    }

    private sealed class DatabaseConfigurationPayload
    {
        public ConnectionStringsSection ConnectionStrings { get; set; } = new();
    }

    private sealed class ConnectionStringsSection
    {
        public string Default { get; set; } = string.Empty;
    }
}
