using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.Secrets;

public class ProviderTokenValidator : IProviderTokenValidator
{
    public Task<ProviderType?> ValidateAsync(string token, string host, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult<ProviderType?>(null);
        }

        ProviderType? providerFromHost = TryParseFromHost(host);
        if (providerFromHost.HasValue)
        {
            return Task.FromResult<ProviderType?>(providerFromHost);
        }

        ProviderType? providerFromToken = TryParseFromToken(token);
        return Task.FromResult(providerFromToken);
    }

    private static ProviderType? TryParseFromHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        string normalized = host.Trim().ToLowerInvariant();
        if (normalized.Contains("github"))
        {
            return ProviderType.Github;
        }

        if (normalized.Contains("gitlab"))
        {
            return ProviderType.Gitlab;
        }

        if (normalized.Contains("bitbucket"))
        {
            return ProviderType.Bitbucket;
        }

        return null;
    }

    private static ProviderType? TryParseFromToken(string token)
    {
        if (token.StartsWith("ghp_", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("gho_", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("github_pat_", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderType.Github;
        }

        if (token.StartsWith("glpat-", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("gl", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderType.Gitlab;
        }

        if (token.StartsWith("bb", StringComparison.OrdinalIgnoreCase)
            || token.Contains("bitbucket", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderType.Bitbucket;
        }

        return null;
    }
}
