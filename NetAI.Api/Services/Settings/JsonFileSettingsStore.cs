using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NetAI.Api.Services.Settings;

public class JsonFileSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonFileSettingsStore> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonFileSettingsStore(ILogger<JsonFileSettingsStore> logger, IConfiguration configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePath = ResolvePath(configuration);

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }

    public async Task<StoredSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            await using FileStream stream = File.OpenRead(_filePath);
            StoredSettings settings = await JsonSerializer.DeserializeAsync<StoredSettings>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);
            return settings?.Copy();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize settings from {Path}", _filePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read settings from {Path}", _filePath);
            return null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task StoreAsync(StoredSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream stream = new(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, settings.Copy(), _serializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to persist settings to {Path}", _filePath);
            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static string ResolvePath(IConfiguration configuration)
    {
        string configuredPath = Environment.GetEnvironmentVariable("OPENHANDS_SETTINGS_PATH")
                                ?? configuration?["Storage:SettingsPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string baseDirectory = Environment.GetEnvironmentVariable("OPENHANDS_HOME")
            ?? configuration?["Storage:RootPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openhands");

        return Path.Combine(baseDirectory, "settings.json");
    }
}
