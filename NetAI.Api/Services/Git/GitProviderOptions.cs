using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Services.Git;

public class GitProviderOptions
{
    public GitHubOptions GitHub { get; set; } = new();
}

public class GitHubOptions
{
    private const string DefaultProductName = "NetAI";

    /// <summary>
    ///     Product header used when creating Octokit clients. Defaults to "NetAI" when not provided.
    /// </summary>
    [Required]
    public string ProductHeader { get; set; } = DefaultProductName;

    /// <summary>
    ///     Personal access token used to authenticate with GitHub. Optional for public repositories.
    /// </summary>
    public string PersonalAccessToken { get; set; }

    /// <summary>
    ///     Maximum number of retries performed when encountering GitHub rate limiting.
    /// </summary>
    [Range(0, 10)]
    public int RateLimitRetryCount { get; set; }
        = 3;

    /// <summary>
    ///     Additional delay (in seconds) added before retrying after a rate limit exception if the
    ///     rate limit reset timestamp is not available.
    /// </summary>
    [Range(0, 300)]
    public int RateLimitFallbackDelaySeconds { get; set; }
        = 5;
}
