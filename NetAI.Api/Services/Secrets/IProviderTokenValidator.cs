using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.Secrets;

public interface IProviderTokenValidator
{
    Task<ProviderType?> ValidateAsync(string token, string host, CancellationToken cancellationToken = default);
}
