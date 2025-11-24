namespace NetAI.Api.Services.Keys;

public class InMemoryApiKeyStore : IApiKeyStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Dictionary<Guid, ApiKeyRecord> _records = new();
    private readonly Dictionary<string, Guid> _hashIndex = new(StringComparer.Ordinal);

    public async Task<IReadOnlyCollection<ApiKeyRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _records.Values
                .Select(static record => record.Copy())
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ApiKeyRecord> TryGetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _records.TryGetValue(id, out ApiKeyRecord record)
                ? record.Copy()
                : null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ApiKeyRecord> TryGetByHashAsync(string hashedKey, CancellationToken cancellationToken = default)
    {
        if (hashedKey is null)
        {
            throw new ArgumentNullException(nameof(hashedKey));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hashIndex.TryGetValue(hashedKey, out Guid id) && _records.TryGetValue(id, out ApiKeyRecord record))
            {
                return record.Copy();
            }

            return null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task AddAsync(ApiKeyRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _records[record.Id] = record.Copy();
            _hashIndex[record.HashedKey] = record.Id;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> TryRemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_records.TryGetValue(id, out ApiKeyRecord existing))
            {
                return false;
            }

            _records.Remove(id);
            _hashIndex.Remove(existing.HashedKey);
            return true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> TryUpdateAsync(ApiKeyRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_records.ContainsKey(record.Id))
            {
                return false;
            }

            _records[record.Id] = record.Copy();
            _hashIndex[record.HashedKey] = record.Id;
            return true;
        }
        finally
        {
            _mutex.Release();
        }
    }
}
