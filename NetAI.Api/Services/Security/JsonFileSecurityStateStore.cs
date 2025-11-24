using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace NetAI.Api.Services.Security;

public class JsonFileSecurityStateStore : ISecurityStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath;
    private readonly ILogger<JsonFileSecurityStateStore> _logger;
    private SecurityStateRecord _cached;

    public JsonFileSecurityStateStore(ILogger<JsonFileSecurityStateStore> logger)
    {
        _logger = logger;
        string baseDirectory = AppContext.BaseDirectory;
        _filePath = Path.Combine(baseDirectory, "data", "security", "security-state.json");
    }

    public async Task<SecurityStateRecord> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached.Copy();
            }

            if (File.Exists(_filePath))
            {
                try
                {
                    await using FileStream readStream = File.OpenRead(_filePath);
                    SecurityStateRecord state = await JsonSerializer.DeserializeAsync<SecurityStateRecord>(readStream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                    if (state is not null)
                    {
                        _cached = state;
                        return state.Copy();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize security state from {FilePath}", _filePath);
                }
            }

            SecurityStateRecord fallback = SecurityStateRecord.CreateDefault();
            await PersistStateAsync(fallback, cancellationToken).ConfigureAwait(false);
            return fallback.Copy();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task StoreAsync(SecurityStateRecord state, CancellationToken cancellationToken = default)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task PersistStateAsync(SecurityStateRecord state, CancellationToken cancellationToken)
    {
        try
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream writeStream = new(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(writeStream, state, SerializerOptions, cancellationToken).ConfigureAwait(false);
            _cached = state.Copy();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist security state to {FilePath}", _filePath);
            throw;
        }
    }
}
