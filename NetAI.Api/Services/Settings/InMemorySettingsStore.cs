using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetAI.Api.Services.Settings;

public class InMemorySettingsStore : ISettingsStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private StoredSettings _settings;

    public async Task<StoredSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _settings?.Copy();
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
            _settings = settings.Copy();
        }
        finally
        {
            _mutex.Release();
        }
    }
}
