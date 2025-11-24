using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NetAI.Api.Models.Git;
using NetAI.Api.Models.User;
using Octokit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NetAI.Api.Services.Git;

public class GitIntegrationService : IGitIntegrationService
{
    private const int DefaultPerPage = 30;
    private const int MaxPerPage = 100;
    private const int BranchSearchPageSize = 100;
    private const string GitHubProviderName = "github";

    private static readonly Regex FrontMatterRegex = new(
        "^---\\s*\r?\n(.*?)\r?\n---\\s*\r?\n?",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly IDeserializer FrontMatterDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly ILogger<GitIntegrationService> _logger;
    private readonly IMicroagentContentClient _microagentContentClient;
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IOptionsMonitor<GitProviderOptions> _options;

    public GitIntegrationService(
        ILogger<GitIntegrationService> logger,
        IMicroagentContentClient microagentContentClient,
        IGitHubClientFactory clientFactory,
        IOptionsMonitor<GitProviderOptions> options)
    {
        _logger = logger;
        _microagentContentClient = microagentContentClient;
        _clientFactory = clientFactory;
        _options = options;
    }

    public async Task<GitUserDto> GetUserInfoAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            User user = await ExecuteWithRetry(() => client.User.Current(), cancellationToken);

            return new GitUserDto
            {
                Id = user.Id.ToString(CultureInfo.InvariantCulture),
                Login = user.Login ?? string.Empty,
                AvatarUrl = user.AvatarUrl ?? string.Empty,
                Company = user.Company,
                Name = user.Name,
                Email = user.Email
            };
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve GitHub user profile. Token is missing or invalid.");
            throw new GitAuthorizationException("GitHub authentication failed. Please verify your personal access token.");
        }
    }

    public async Task<IReadOnlyList<GitRepositoryDto>> GetUserRepositoriesAsync(
        string selectedProvider,
        int page,
        int perPage,
        string sort,
        string installationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGitHubProvider(selectedProvider);

        int normalizedPerPage = NormalizePerPage(perPage);
        int pageNumber = Math.Max(page, 1);

        if (!string.IsNullOrWhiteSpace(installationId))
        {
            _logger.LogDebug(
                "Ignoring unsupported installation id {InstallationId} for GitHub repository listing.",
                installationId);
        }

        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        var request = new RepositoryRequest
        {
            Sort = MapRepositorySort(sort),
            Direction = SortDirection.Descending,
            Affiliation = RepositoryAffiliation.Owner
                | RepositoryAffiliation.Collaborator
                | RepositoryAffiliation.OrganizationMember
        };

        var options = new ApiOptions
        {
            PageCount = 1,
            PageSize = normalizedPerPage,
            StartPage = pageNumber
        };

        try
        {
            IReadOnlyList<Repository> repositories = await ExecuteWithRetry(
                () => client.Repository.GetAllForCurrent(request, options),
                cancellationToken);

            bool hasNext = repositories.Count == normalizedPerPage;
            string linkHeader = hasNext
                ? BuildLinkHeader(pageNumber + 1, normalizedPerPage)
                : null;

            return repositories
                .Select((repo, index) => MapRepository(repo, index == 0 ? linkHeader : null))
                .ToList();
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve repositories for the authenticated GitHub user.");
            throw new GitAuthorizationException("GitHub authentication failed. Please verify your personal access token.");
        }
    }

    public async Task<IReadOnlyList<GitRepositoryDto>> SearchRepositoriesAsync(
        string query,
        int perPage,
        string selectedProvider,
        string sort,
        string order,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGitHubProvider(selectedProvider);

        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<GitRepositoryDto>();
        }

        int normalizedPerPage = NormalizePerPage(perPage);
        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        var request = new SearchRepositoriesRequest(query)
        {
            PerPage = normalizedPerPage,
            Page = 1,
            Order = MapSortDirection(order)
        };

        RepoSearchSort? sortField = MapSearchSort(sort);
        if (sortField.HasValue)
        {
            request.SortField = sortField.Value;
        }

        try
        {
            SearchRepositoryResult result = await ExecuteWithRetry(
                () => client.Search.SearchRepo(request),
                cancellationToken);

            return result.Items
                .Select(repo => MapRepository(repo, null))
                .ToList();
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "Failed to search repositories for the authenticated GitHub user.");
            throw new GitAuthorizationException("GitHub authentication failed. Please verify your personal access token.");
        }
    }

    public async Task<PaginatedBranchesResponseDto> GetRepositoryBranchesAsync(
        string repository,
        int page,
        int perPage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(repository))
        {
            throw new ArgumentException("Repository parameter is required.", nameof(repository));
        }

        (string owner, string name) = ParseRepository(repository);
        int normalizedPerPage = NormalizePerPage(perPage);
        int pageNumber = Math.Max(page, 1);

        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        var options = new ApiOptions
        {
            PageCount = 1,
            PageSize = normalizedPerPage,
            StartPage = pageNumber
        };

        try
        {
            IReadOnlyList<Branch> branches = await ExecuteWithRetry(
                () => client.Repository.Branch.GetAll(owner, name, options),
                cancellationToken);

            List<BranchDto> items = branches
                .Select(MapBranch)
                .ToList();

            bool hasNext = branches.Count == normalizedPerPage;

            return new PaginatedBranchesResponseDto
            {
                Branches = items,
                HasNextPage = hasNext,
                CurrentPage = pageNumber,
                PerPage = normalizedPerPage,
                TotalCount = null
            };
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Repository {Repository} was not found when fetching branches.", repository);
            throw new ArgumentException("Repository could not be found.", nameof(repository));
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "Unauthorized when retrieving branches for {Repository}.", repository);
            throw new GitAuthorizationException("GitHub authentication failed. Please verify your personal access token.");
        }
    }

    public async Task<IReadOnlyList<BranchDto>> SearchBranchesAsync(
        string repository,
        string query,
        int perPage,
        string selectedProvider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(repository))
        {
            throw new ArgumentException("Repository parameter is required.", nameof(repository));
        }

        if (!string.IsNullOrWhiteSpace(selectedProvider))
        {
            EnsureGitHubProvider(selectedProvider);
        }

        (string owner, string name) = ParseRepository(repository);
        string normalizedQuery = (query ?? string.Empty).Trim();
        int normalizedPerPage = NormalizePerPage(perPage);

        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(normalizedQuery))
        {
            PaginatedBranchesResponseDto firstPage = await GetRepositoryBranchesAsync(
                repository,
                1,
                normalizedPerPage,
                cancellationToken);

            return firstPage.Branches;
        }

        var results = new List<BranchDto>();
        int currentPage = 1;

        while (results.Count < normalizedPerPage)
        {
            var options = new ApiOptions
            {
                PageCount = 1,
                PageSize = BranchSearchPageSize,
                StartPage = currentPage
            };

            IReadOnlyList<Branch> branches;
            try
            {
                branches = await ExecuteWithRetry(
                    () => client.Repository.Branch.GetAll(owner, name, options),
                    cancellationToken);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Repository {Repository} was not found when searching branches.", repository);
                throw new ArgumentException("Repository could not be found.", nameof(repository));
            }
            catch (AuthorizationException ex)
            {
                _logger.LogWarning(ex, "Unauthorized when searching branches for {Repository}.", repository);
                throw new GitAuthorizationException("GitHub authentication failed. Please verify your personal access token.");
            }

            if (branches.Count == 0)
            {
                break;
            }

            foreach (Branch branch in branches)
            {
                if (branch.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(MapBranch(branch));
                    if (results.Count >= normalizedPerPage)
                    {
                        break;
                    }
                }
            }

            if (branches.Count < BranchSearchPageSize)
            {
                break;
            }

            currentPage++;
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetInstallationIdsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGitHubProvider(provider);

        IGitHubClient client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            IReadOnlyList<Octokit.Installation> installations = await ExecuteWithRetry(
                () => GetUserInstallationsAsync(client),
                cancellationToken);

            return installations
                .Select(installation => installation.Id.ToString(CultureInfo.InvariantCulture))
                .ToList();
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve GitHub installations for the authenticated user.");
            throw new GitAuthorizationException("GitHub authentication failed. Please verify your personal access token.");
        }
    }

    public async Task<IReadOnlyList<RepositoryMicroagentDto>> GetRepositoryMicroagentsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
        {
            throw new ArgumentException("Owner and repository parameters are required.");
        }

        IReadOnlyList<MicroagentFileDescriptor> files = await _microagentContentClient
            .GetMicroagentFilesAsync(owner, repository, cancellationToken);

        return files
            .Select(file => new RepositoryMicroagentDto
            {
                Name = file.Name,
                Path = file.Path,
                GitProvider = file.GitProvider,
                CreatedAt = file.CreatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
            })
            .ToList();
    }

    public async Task<MicroagentContentResponseDto> GetRepositoryMicroagentContentAsync(
        string owner,
        string repository,
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
        {
            throw new ArgumentException("Owner and repository parameters are required.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("file_path query parameter is required.", nameof(filePath));
        }

        MicroagentFileContent fileContent = await _microagentContentClient
            .GetMicroagentFileContentAsync(owner, repository, filePath, cancellationToken);

        (string content, IReadOnlyList<string> triggers) = ParseFrontMatter(fileContent.Content);

        return new MicroagentContentResponseDto
        {
            Content = content,
            Path = fileContent.Path,
            GitProvider = fileContent.GitProvider,
            Triggers = triggers
        };
    }

    public Task<IReadOnlyList<SuggestedTaskDto>> GetSuggestedTasksAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<SuggestedTaskDto>>(Array.Empty<SuggestedTaskDto>());
    }

    private (string Content, IReadOnlyList<string> Triggers) ParseFrontMatter(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return (string.Empty, Array.Empty<string>());
        }

        Match match = FrontMatterRegex.Match(content);
        if (!match.Success)
        {
            return (content, Array.Empty<string>());
        }

        string yaml = match.Groups[1].Value;
        try
        {
            Dictionary<string, object> metadata = FrontMatterDeserializer
                                                      .Deserialize<Dictionary<string, object>>(yaml)
                                                  ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            metadata = new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase);

            metadata.TryGetValue("triggers", out object triggerNode);
            IReadOnlyList<string> triggers = ExtractTriggers(triggerNode);

            string remaining = content[match.Length..];
            remaining = remaining.TrimStart('\r', '\n');

            return (remaining, triggers);
        }
        catch (YamlException ex)
        {
            _logger.LogWarning(ex, "Failed to parse microagent YAML front matter.");
            return (content, Array.Empty<string>());
        }
    }

    private static IReadOnlyList<string> ExtractTriggers(object triggerNode)
    {
        if (triggerNode is null)
        {
            return Array.Empty<string>();
        }

        if (triggerNode is string triggerString)
        {
            return triggerString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        if (triggerNode is IEnumerable enumerable)
        {
            var triggers = new List<string>();
            foreach (object item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                if (item is string itemString)
                {
                    string trimmed = itemString.Trim();
                    if (trimmed.Length > 0)
                    {
                        triggers.Add(trimmed);
                    }
                }
                else
                {
                    string text = item.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        triggers.Add(text.Trim());
                    }
                }
            }

            return triggers;
        }

        string fallback = triggerNode.ToString();
        return string.IsNullOrWhiteSpace(fallback)
            ? Array.Empty<string>()
            : new[] { fallback.Trim() };
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

    private static void EnsureGitHubProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new GitAuthorizationException("Git provider token required.");
        }

        if (!string.Equals(provider, GitHubProviderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitAuthorizationException($"Unsupported git provider '{provider}'.");
        }
    }

    private static int NormalizePerPage(int perPage)
    {
        if (perPage <= 0)
        {
            return DefaultPerPage;
        }

        return Math.Min(perPage, MaxPerPage);
    }

    private static string BuildLinkHeader(int nextPage, int perPage)
    {
        if (nextPage <= 1)
        {
            return null;
        }

        return $"<https://api.github.com/user/repos?page={nextPage}&per_page={perPage}>; rel=\"next\"";
    }

    private static GitRepositoryDto MapRepository(Repository repo, string linkHeader)
    {
        return new GitRepositoryDto
        {
            Id = repo.Id.ToString(CultureInfo.InvariantCulture),
            FullName = repo.FullName,
            GitProvider = GitHubProviderName,
            IsPublic = !repo.Private,
            StargazersCount = repo.StargazersCount,
            LinkHeader = linkHeader,
            PushedAt = repo.PushedAt?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            OwnerType = repo.Owner?.Type.ToString(),
            MainBranch = repo.DefaultBranch
        };
    }

    private static BranchDto MapBranch(Branch branch)
    {
        string lastPush = TryGetBranchLastPushDate(branch);

        return new BranchDto
        {
            Name = branch.Name,
            CommitSha = branch.Commit?.Sha ?? string.Empty,
            Protected = branch.Protected,
            LastPushDate = lastPush
        };
    }

    private static RepositorySort MapRepositorySort(string sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "created" => RepositorySort.Created,
            "updated" => RepositorySort.Updated,
            "full_name" => RepositorySort.FullName,
            _ => RepositorySort.Pushed
        };
    }

    private static RepoSearchSort? MapSearchSort(string sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "stars" => RepoSearchSort.Stars,
            "forks" => RepoSearchSort.Forks,
            "updated" => RepoSearchSort.Updated,
            _ => null
        };
    }

    private static SortDirection MapSortDirection(string order)
    {
        return string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Ascending
            : SortDirection.Descending;
    }

    private static string TryGetBranchLastPushDate(Branch branch)
    {
        object commit = branch.Commit;
        if (commit is null)
        {
            return null;
        }

        string formattedDate = TryExtractCommitDate(commit, "Commit", "Author")
                               ?? TryExtractCommitDate(commit, "Commit", "Committer")
                               ?? TryExtractCommitDate(commit, "Author")
                               ?? TryExtractCommitDate(commit, "Committer");

        return formattedDate;
    }

    private static string TryExtractCommitDate(object source, params string[] propertyPath)
    {
        object current = source;
        foreach (string segment in propertyPath)
        {
            if (current is null)
            {
                return null;
            }

            PropertyInfo property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        if (current is DateTimeOffset dto)
        {
            return dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        if (current is DateTime dt)
        {
            return dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private Task<IReadOnlyList<Octokit.Installation>> GetUserInstallationsAsync(IGitHubClient client)
    {
        object gitHubApps = client.GitHubApps;

        if (TryCreateInstallationInvocation(gitHubApps, "GetAllInstallationsForCurrentUser", out Func<Task<object>> directInvocation))
        {
            return UnwrapInstallationsAsync(directInvocation);
        }

        if (TryGetProperty(gitHubApps, "Installations", out object installationsClient) && installationsClient is not null)
        {
            if (TryCreateInstallationInvocation(installationsClient, "GetAllForCurrent", out Func<Task<object>> forCurrentInvocation))
            {
                return UnwrapInstallationsAsync(forCurrentInvocation);
            }

            if (TryCreateInstallationInvocation(installationsClient, "GetAllForCurrentUser", out Func<Task<object>> forCurrentUserInvocation))
            {
                return UnwrapInstallationsAsync(forCurrentUserInvocation);
            }
        }

        throw new NotSupportedException("Octokit client does not expose installation APIs in a recognized format.");
    }

    private static bool TryGetProperty(object target, string propertyName, out object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        value = property?.GetValue(target);
        return property is not null;
    }

    private static bool TryCreateInstallationInvocation(object target, string methodName, out Func<Task<object>> invocation)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        if (method is null)
        {
            invocation = null!;
            return false;
        }

        invocation = () => InvokeInstallationMethodAsync(target, method);
        return true;
    }

    private static async Task<object> InvokeInstallationMethodAsync(object target, MethodInfo method)
    {
        object invocationResult = method.Invoke(target, Array.Empty<object>());
        if (invocationResult is Task task)
        {
            await task.ConfigureAwait(false);
            PropertyInfo resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
            return resultProperty?.GetValue(task);
        }

        return invocationResult;
    }

    private static async Task<IReadOnlyList<Octokit.Installation>> UnwrapInstallationsAsync(Func<Task<object>> invocation)
    {
        object value = await invocation().ConfigureAwait(false);

        if (TryExtractInstallations(value, out IReadOnlyList<Octokit.Installation> installations))
        {
            return installations;
        }

        if (value is not null &&
            TryGetProperty(value, "Body", out object body) &&
            TryExtractInstallations(body, out installations))
        {
            return installations;
        }

        return Array.Empty<Octokit.Installation>();
    }

    private static bool TryExtractInstallations(object candidate, out IReadOnlyList<Octokit.Installation> installations)
    {
        switch (candidate)
        {
            case IReadOnlyList<Octokit.Installation> readOnlyList:
                installations = readOnlyList;
                return true;
            case IEnumerable<Octokit.Installation> enumerable:
                installations = enumerable.ToList();
                return true;
        }

        if (candidate is not null &&
            TryGetProperty(candidate, "Installations", out object installationsProperty))
        {
            switch (installationsProperty)
            {
                case IReadOnlyList<Octokit.Installation> readOnlyList:
                    installations = readOnlyList;
                    return true;
                case IEnumerable<Octokit.Installation> enumerable:
                    installations = enumerable.ToList();
                    return true;
            }
        }

        installations = Array.Empty<Octokit.Installation>();
        return false;
    }

    private static (string Owner, string Name) ParseRepository(string repository)
    {
        string[] parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException("Repository must be in the format 'owner/repo'.", nameof(repository));
        }

        return (parts[0], parts[1]);
    }
}
