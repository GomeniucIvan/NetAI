using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Services.Secrets;

namespace NetAI.Api.Services.Git;

public interface IPullRequestStatusService
{
    Task<bool> IsPullRequestOpenAsync(string repository, int prNumber, ProviderType provider, CancellationToken cancellationToken);
}

public class PullRequestStatusService : IPullRequestStatusService
{
    private readonly ISecretsStore _secretsStore;
    private readonly ILogger<PullRequestStatusService> _logger;

    public PullRequestStatusService(ISecretsStore secretsStore, ILogger<PullRequestStatusService> logger)
    {
        _secretsStore = secretsStore;
        _logger = logger;
    }

    public async Task<bool> IsPullRequestOpenAsync(string repository, int prNumber, ProviderType provider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            UserSecrets secrets = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (secrets is null)
            {
                _logger.LogDebug("No secrets available; including conversation {Repository}#{PrNumber} by default.", repository, prNumber);
                return true;
            }

            if (!secrets.ProviderTokens.TryGetValue(provider, out ProviderTokenInfo token) || string.IsNullOrWhiteSpace(token.Token))
            {
                _logger.LogDebug(
                    "No provider token found for {Provider}; including conversation {Repository}#{PrNumber} by default.",
                    provider,
                    repository,
                    prNumber);
                return true;
            }

            _logger.LogDebug(
                "Assuming pull request {Repository}#{PrNumber} is open for provider {Provider} (token available).",
                repository,
                prNumber,
                provider);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to determine pull request status for {Repository}#{PrNumber}; including conversation by default.",
                repository,
                prNumber);
            return true;
        }
    }
}
