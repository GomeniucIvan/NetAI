using System.Threading;

namespace NetAI.Api.Services.Secrets;

public class InMemorySecretsStore : ISecretsStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private UserSecrets _secrets;

    public async Task<UserSecrets> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _secrets?.Clone();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task StoreAsync(UserSecrets secrets, CancellationToken cancellationToken = default)
    {
        if (secrets is null)
        {
            throw new ArgumentNullException(nameof(secrets));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _secrets = secrets.Clone();
        }
        finally
        {
            _mutex.Release();
        }
    }
}
