namespace NetAI.Api.Services.Settings;

public interface ISettingsStore
{
    Task<StoredSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task StoreAsync(StoredSettings settings, CancellationToken cancellationToken = default);
}
