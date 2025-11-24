using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace NetAI.Api.Services.Git;

public class GitHubMicroagentContentClient : IMicroagentContentClient
{

    //todo setting
    private const string MicroagentRootPath = ".openhands/microagents";
    private const string ProviderName = "github";

    private readonly IGitHubClientFactory _clientFactory;
    private readonly ILogger<GitHubMicroagentContentClient> _logger;
    private readonly IOptionsMonitor<GitProviderOptions> _options;

    public GitHubMicroagentContentClient(
        IGitHubClientFactory clientFactory,
        ILogger<GitHubMicroagentContentClient> logger,
        IOptionsMonitor<GitProviderOptions> options)
    {
        _clientFactory = clientFactory;
        _logger = logger;
        _options = options;
    }

    public async Task<IReadOnlyList<MicroagentFileDescriptor>> GetMicroagentFilesAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        var descriptors = new List<MicroagentFileDescriptor>();

        await foreach (RepositoryContent content in EnumerateMicroagentFilesAsync(client, owner, repository, MicroagentRootPath, cancellationToken))
        {
            DateTimeOffset? createdAt = await GetLastCommitDateAsync(client, owner, repository, content.Path, cancellationToken);
            descriptors.Add(new MicroagentFileDescriptor(
                content.Name,
                content.Path,
                createdAt,
                ProviderName));
        }

        return descriptors;
    }

    public async Task<MicroagentFileContent> GetMicroagentFileContentAsync(
        string owner,
        string repository,
        string path,
        CancellationToken cancellationToken)
    {
        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        RepositoryContent fileContent;

        try
        {
            IReadOnlyList<RepositoryContent> contents = await ExecuteWithRetry(
                () => client.Repository.Content.GetAllContents(owner, repository, path),
                cancellationToken);

            fileContent = contents.FirstOrDefault()
                ?? throw new GitResourceNotFoundException($"Microagent '{path}' was not found.");
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Microagent file {MicroagentPath} was not found in {Owner}/{Repository}.", path, owner, repository);
            throw new GitResourceNotFoundException($"Microagent '{path}' was not found.");
        }

        if (fileContent.Type != ContentType.File)
        {
            throw new GitResourceNotFoundException($"Requested microagent '{path}' is not a file.");
        }

        string decodedContent = DecodeContent(fileContent);
        DateTimeOffset? lastModifiedAt = await GetLastCommitDateAsync(client, owner, repository, path, cancellationToken);

        return new MicroagentFileContent(
            path,
            decodedContent,
            ProviderName,
            lastModifiedAt);
    }

    private async IAsyncEnumerable<RepositoryContent> EnumerateMicroagentFilesAsync(
        IGitHubClient client,
        string owner,
        string repository,
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<RepositoryContent> contents;
        try
        {
            contents = await ExecuteWithRetry(
                () => client.Repository.Content.GetAllContents(owner, repository, path),
                cancellationToken);
        }
        catch (NotFoundException)
        {
            // Directory doesn't exist; return no files.
            yield break;
        }

        foreach (RepositoryContent item in contents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.Type == ContentType.File
                && item.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                yield return item;
            }
            else if (item.Type == ContentType.Dir)
            {
                await foreach (RepositoryContent descendant in EnumerateMicroagentFilesAsync(client, owner, repository, item.Path, cancellationToken))
                {
                    yield return descendant;
                }
            }
        }
    }

    private async Task<DateTimeOffset?> GetLastCommitDateAsync(
        IGitHubClient client,
        string owner,
        string repository,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new CommitRequest
            {
                Path = path
            };

            var options = new ApiOptions
            {
                PageSize = 1,
                PageCount = 1
            };

            IReadOnlyList<GitHubCommit> commits = await ExecuteWithRetry(
                () => client.Repository.Commit.GetAll(owner, repository, request, options),
                cancellationToken);

            GitHubCommit commit = commits.FirstOrDefault();
            return commit?.Commit?.Author?.Date;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        GitHubOptions gitHubOptions = _options.CurrentValue.GitHub;
        int retriesRemaining = gitHubOptions.RateLimitRetryCount;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action();
            }
            catch (RateLimitExceededException ex) when (retriesRemaining-- > 0)
            {
                TimeSpan delay = CalculateDelay(ex, gitHubOptions);
                _logger.LogWarning(ex, "GitHub rate limit exceeded. Retrying after {Delay}.", delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (AbuseException ex) when (retriesRemaining-- > 0)
            {
                TimeSpan delay = ex.RetryAfterSeconds.HasValue
                    ? TimeSpan.FromSeconds(ex.RetryAfterSeconds.Value)
                    : TimeSpan.FromSeconds(gitHubOptions.RateLimitFallbackDelaySeconds);
                _logger.LogWarning(ex, "GitHub abuse detection triggered. Retrying after {Delay}.", delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static TimeSpan CalculateDelay(RateLimitExceededException exception, GitHubOptions options)
    {
        if (exception.Reset > DateTimeOffset.UtcNow)
        {
            return exception.Reset - DateTimeOffset.UtcNow;
        }

        return TimeSpan.FromSeconds(Math.Max(1, options.RateLimitFallbackDelaySeconds));
    }

    private static string DecodeContent(RepositoryContent content)
    {
        if (!string.Equals(content.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            return content.Content ?? string.Empty;
        }

        if (string.IsNullOrEmpty(content.Content))
        {
            return string.Empty;
        }

        byte[] bytes = Convert.FromBase64String(content.Content);
        return Encoding.UTF8.GetString(bytes);
    }
}
