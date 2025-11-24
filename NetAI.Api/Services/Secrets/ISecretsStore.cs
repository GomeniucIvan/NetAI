namespace NetAI.Api.Services.Secrets;

public interface ISecretsStore
{
    Task<UserSecrets> LoadAsync(CancellationToken cancellationToken = default);

    Task StoreAsync(UserSecrets secrets, CancellationToken cancellationToken = default);
}
