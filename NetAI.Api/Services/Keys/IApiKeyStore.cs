namespace NetAI.Api.Services.Keys;

public interface IApiKeyStore
{
    Task<IReadOnlyCollection<ApiKeyRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ApiKeyRecord> TryGetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiKeyRecord> TryGetByHashAsync(string hashedKey, CancellationToken cancellationToken = default);

    Task AddAsync(ApiKeyRecord record, CancellationToken cancellationToken = default);

    Task<bool> TryRemoveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> TryUpdateAsync(ApiKeyRecord record, CancellationToken cancellationToken = default);
}
