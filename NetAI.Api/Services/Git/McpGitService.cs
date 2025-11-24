using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Mcp;
using NetAI.Api.Services.Secrets;
using Octokit;

namespace NetAI.Api.Services.Git;

public interface IMcpGitService
{
    Task<string> CreatePullRequestAsync(
        CreatePullRequestRequest request,
        string conversationId,
        CancellationToken cancellationToken);

    Task<string> CreateMergeRequestAsync(
        CreateMergeRequestRequest request,
        string conversationId,
        CancellationToken cancellationToken);

    Task<string> CreateBitbucketPullRequestAsync(
        CreateBitbucketPullRequestRequest request,
        string conversationId,
        CancellationToken cancellationToken);
}

public class McpGitService : IMcpGitService
{
    private const string DefaultWebHost = "app.all-hands.dev";
    private static readonly Regex PullRequestRegex = new("pull/(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MergeRequestRegex = new("merge_requests/(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IGitHubClientFactory _gitHubClientFactory;
    private readonly ISecretsStore _secretsStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConversationRepository _conversationRepository;
    private readonly ILogger<McpGitService> _logger;

    public McpGitService(
        IGitHubClientFactory gitHubClientFactory,
        ISecretsStore secretsStore,
        IHttpClientFactory httpClientFactory,
        IConversationRepository conversationRepository,
        ILogger<McpGitService> logger)
    {
        _gitHubClientFactory = gitHubClientFactory;
        _secretsStore = secretsStore;
        _httpClientFactory = httpClientFactory;
        _conversationRepository = conversationRepository;
        _logger = logger;
    }

    public async Task<string> CreatePullRequestAsync(
        CreatePullRequestRequest request,
        string conversationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Repository))
        {
            throw new ArgumentException("Repository name is required", nameof(request));
        }

        (string owner, string repo) = ParseRepository(request.Repository);
        IGitHubClient client = await _gitHubClientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);

        string body = string.IsNullOrWhiteSpace(request.Body)
            ? $"Merging changes from {request.SourceBranch} into {request.TargetBranch}"
            : request.Body;

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            try
            {
                User user = await client.User.Current().ConfigureAwait(false);
                body = AppendConversationLink(body!, user.Login, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append conversation link for GitHub pull request");
            }
        }

        var newPr = new NewPullRequest(request.Title, request.SourceBranch, request.TargetBranch)
        {
            Body = body,
            Draft = request.Draft
        };

        PullRequest created = await client.PullRequest
            .Create(owner, repo, newPr)
            .ConfigureAwait(false);

        if (request.Labels is { Count: > 0 })
        {
            try
            {
                await client.Issue.Labels
                    .AddToIssue(owner, repo, created.Number, request.Labels.ToArray())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply labels to pull request {PullRequestUrl}", created.HtmlUrl);
            }
        }

        await RecordPullRequestAsync(conversationId, created.HtmlUrl, cancellationToken).ConfigureAwait(false);
        return created.HtmlUrl;
    }

    public async Task<string> CreateMergeRequestAsync(
        CreateMergeRequestRequest request,
        string conversationId,
        CancellationToken cancellationToken)
    {
        ProviderTokenInfo token = await ResolveProviderTokenAsync(ProviderType.Gitlab, cancellationToken).ConfigureAwait(false);
        string apiToken = RequireToken(token, ProviderType.Gitlab);
        Uri apiBase = BuildGitLabApiBase(token.Host);

        string description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Merging changes from {request.SourceBranch} into {request.TargetBranch}"
            : request.Description!;

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            try
            {
                string username = await GetGitLabUsernameAsync(apiBase, apiToken, cancellationToken).ConfigureAwait(false);
                description = AppendConversationLink(description, username, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append conversation link for GitLab merge request");
            }
        }

        string projectId = request.ResolveProjectId();
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("GitLab project id is required", nameof(request));
        }

        var payload = new Dictionary<string, object>
        {
            ["source_branch"] = request.SourceBranch,
            ["target_branch"] = request.TargetBranch,
            ["title"] = request.Title,
            ["description"] = description
        };

        if (request.Labels is { Count: > 0 })
        {
            payload["labels"] = string.Join(',', request.Labels);
        }

        Uri requestUri = new(apiBase, $"projects/{projectId}/merge_requests");
        using HttpClient client = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using HttpResponseMessage response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GitLab merge request creation failed with status {StatusCode}: {Body}",
                response.StatusCode,
                responseBody);
            throw new InvalidOperationException($"GitLab merge request creation failed: {response.StatusCode}");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        string mergeRequestUrl = document.RootElement.TryGetProperty("web_url", out JsonElement urlElement)
            ? urlElement.GetString()
            : null;

        await RecordPullRequestAsync(conversationId, mergeRequestUrl, cancellationToken).ConfigureAwait(false);
        return mergeRequestUrl ?? string.Empty;
    }

    public async Task<string> CreateBitbucketPullRequestAsync(
        CreateBitbucketPullRequestRequest request,
        string conversationId,
        CancellationToken cancellationToken)
    {
        ProviderTokenInfo token = await ResolveProviderTokenAsync(ProviderType.Bitbucket, cancellationToken).ConfigureAwait(false);
        string apiToken = RequireToken(token, ProviderType.Bitbucket);
        Uri apiBase = BuildBitbucketApiBase(token.Host);

        string description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Merging changes from {request.SourceBranch} into {request.TargetBranch}"
            : request.Description!;

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            try
            {
                string username = await GetBitbucketUsernameAsync(apiBase, apiToken, cancellationToken).ConfigureAwait(false);
                description = AppendConversationLink(description, username, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append conversation link for Bitbucket pull request");
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["title"] = request.Title,
            ["description"] = description,
            ["source"] = new Dictionary<string, object>
            {
                ["branch"] = new Dictionary<string, string>
                {
                    ["name"] = request.SourceBranch
                }
            },
            ["destination"] = new Dictionary<string, object>
            {
                ["branch"] = new Dictionary<string, string>
                {
                    ["name"] = request.TargetBranch
                }
            },
            ["close_source_branch"] = false,
            ["draft"] = request.Draft
        };

        Uri requestUri = new(apiBase, $"repositories/{request.Repository}/pullrequests");
        using HttpClient client = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };

        ApplyBitbucketAuthorization(httpRequest, apiToken);

        using HttpResponseMessage response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Bitbucket pull request creation failed with status {StatusCode}: {Body}",
                response.StatusCode,
                responseBody);
            throw new InvalidOperationException($"Bitbucket pull request creation failed: {response.StatusCode}");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        string pullRequestUrl = null;
        if (document.RootElement.TryGetProperty("links", out JsonElement links)
            && links.TryGetProperty("html", out JsonElement html)
            && html.TryGetProperty("href", out JsonElement href))
        {
            pullRequestUrl = href.GetString();
        }

        await RecordPullRequestAsync(conversationId, pullRequestUrl, cancellationToken).ConfigureAwait(false);
        return pullRequestUrl ?? string.Empty;
    }

    private static (string Owner, string Repository) ParseRepository(string repository)
    {
        string[] parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new ArgumentException("Repository must be in the format 'owner/repo'.", nameof(repository));
        }

        string owner = parts[^2];
        string repo = parts[^1];
        return (owner, repo);
    }

    private async Task<ProviderTokenInfo> ResolveProviderTokenAsync(ProviderType providerType, CancellationToken cancellationToken)
    {
        UserSecrets secrets = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (secrets is null || !secrets.ProviderTokens.TryGetValue(providerType, out ProviderTokenInfo info))
        {
            throw new GitAuthorizationException($"Provider token for {providerType} is not configured.");
        }

        return info;
    }

    private static string RequireToken(ProviderTokenInfo info, ProviderType provider)
    {
        string token = info.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new GitAuthorizationException($"Provider token for {provider} is not configured.");
        }

        return token.Trim();
    }

    private static Uri BuildGitLabApiBase(string host)
    {
        string baseHost = string.IsNullOrWhiteSpace(host) ? "https://gitlab.com/" : host.Trim();
        if (!baseHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !baseHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseHost = $"https://{baseHost}";
        }

        if (!baseHost.EndsWith('/'))
        {
            baseHost += '/';
        }

        var root = new Uri(baseHost, UriKind.Absolute);
        return new Uri(root, "api/v4/");
    }

    private static Uri BuildBitbucketApiBase(string host)
    {
        string domain = string.IsNullOrWhiteSpace(host) ? "bitbucket.org" : host.Trim();

        if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(domain, UriKind.Absolute);
            string scheme = uri.Scheme;
            domain = uri.Host;
            string prefix = domain.StartsWith("api.", StringComparison.OrdinalIgnoreCase) ? string.Empty : "api.";
            return new Uri($"{scheme}://{prefix}{domain}/2.0/");
        }

        string trimmed = domain.StartsWith("api.", StringComparison.OrdinalIgnoreCase)
            ? domain
            : $"api.{domain}";
        return new Uri($"https://{trimmed}/2.0/");
    }

    private static string AppendConversationLink(string body, string username, string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return body;
        }

        string host = Environment.GetEnvironmentVariable("WEB_HOST")?.Trim() ?? DefaultWebHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = DefaultWebHost;
        }

        string baseUrl = host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? host.TrimEnd('/')
            : $"https://{host.TrimEnd('/')}";

        string conversationUrl = $"{baseUrl}/conversations/{conversationId}";
        string mention = string.IsNullOrWhiteSpace(username) ? string.Empty : $"@{username} ";
        string link = $"{mention}can click here to [continue refining the PR]({conversationUrl})";
        return string.IsNullOrWhiteSpace(body)
            ? link
            : $"{body}\n\n{link}";
    }

    private async Task RecordPullRequestAsync(string conversationId, string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        int? prNumber = ExtractPullRequestNumber(url);
        if (prNumber is null)
        {
            return;
        }

        try
        {
            var record = await _conversationRepository
                .GetConversationAsync(conversationId, includeDetails: true, cancellationToken)
                .ConfigureAwait(false);

            if (record is null)
            {
                _logger.LogDebug("Conversation {ConversationId} not found when recording PR number", conversationId);
                return;
            }

            if (!record.PullRequestNumbers.Contains(prNumber.Value))
            {
                record.PullRequestNumbers.Add(prNumber.Value);
                await _conversationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record pull request {PullRequest} for conversation {ConversationId}", prNumber, conversationId);
        }
    }

    private static int? ExtractPullRequestNumber(string url)
    {
        Match pullMatch = PullRequestRegex.Match(url);
        if (pullMatch.Success && int.TryParse(pullMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int pull))
        {
            return pull;
        }

        Match mergeMatch = MergeRequestRegex.Match(url);
        if (mergeMatch.Success && int.TryParse(mergeMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int merge))
        {
            return merge;
        }

        return null;
    }

    private async Task<string> GetGitLabUsernameAsync(Uri apiBase, string token, CancellationToken cancellationToken)
    {
        Uri requestUri = new(apiBase, "user");
        using HttpClient client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("username", out JsonElement element)
            ? element.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task<string> GetBitbucketUsernameAsync(Uri apiBase, string token, CancellationToken cancellationToken)
    {
        Uri requestUri = new(apiBase, "user");
        using HttpClient client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        ApplyBitbucketAuthorization(request, token);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("username", out JsonElement element)
            ? element.GetString() ?? string.Empty
            : string.Empty;
    }

    private static void ApplyBitbucketAuthorization(HttpRequestMessage request, string token)
    {
        if (token.Contains(':', StringComparison.Ordinal))
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
