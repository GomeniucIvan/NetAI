using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Services.Secrets;
using Octokit;

namespace NetAI.Api.Services.Git;

public class GitHubClientFactory : IGitHubClientFactory
{
    private readonly ILogger<GitHubClientFactory> _logger;
    private readonly IOptionsMonitor<GitProviderOptions> _options;
    private readonly ISecretsStore _secretsStore;

    public GitHubClientFactory(
        ILogger<GitHubClientFactory> logger,
        IOptionsMonitor<GitProviderOptions> options,
        ISecretsStore secretsStore)
    {
        _logger = logger;
        _options = options;
        _secretsStore = secretsStore;
    }

    public async Task<IGitHubClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        GitHubOptions gitHubOptions = _options.CurrentValue.GitHub;
        string productHeader = string.IsNullOrWhiteSpace(gitHubOptions.ProductHeader)
            ? "NetAI"
            : gitHubOptions.ProductHeader;

        (string token, Uri baseAddress) = await ResolveCredentialsAsync(gitHubOptions, cancellationToken)
            .ConfigureAwait(false);

        GitHubClient client = baseAddress is not null
            ? new GitHubClient(new ProductHeaderValue(productHeader), baseAddress)
            : new GitHubClient(new ProductHeaderValue(productHeader));

        if (!string.IsNullOrWhiteSpace(token))
        {
            client.Credentials = new Credentials(token);
        }
        else
        {
            _logger.LogDebug("No GitHub personal access token configured; requests will be unauthenticated.");
        }

        return client;
    }

    private async Task<(string Token, Uri BaseAddress)> ResolveCredentialsAsync(
        GitHubOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            UserSecrets secrets = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (secrets is not null
                && secrets.ProviderTokens.TryGetValue(ProviderType.Github, out ProviderTokenInfo tokenInfo))
            {
                string normalizedToken = Normalize(tokenInfo?.Token);
                Uri baseAddress = TryCreateUri(tokenInfo?.Host);

                if (!string.IsNullOrEmpty(normalizedToken))
                {
                    return (normalizedToken, baseAddress);
                }

                if (baseAddress is not null)
                {
                    return (Normalize(options.PersonalAccessToken), baseAddress);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read GitHub credentials from secrets store. Falling back to configuration.");
        }

        return (Normalize(options.PersonalAccessToken), null);
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Uri TryCreateUri(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        string trimmed = host.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri) ? uri : null;
    }
}
