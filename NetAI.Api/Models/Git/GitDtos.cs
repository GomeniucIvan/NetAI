using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Git;

public record class GitRepositoryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("full_name")]
    public string FullName { get; init; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; init; } = "github";

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; init; }
        = true;

    [JsonPropertyName("stargazers_count")]
    public int? StargazersCount { get; init; }
        = null;

    [JsonPropertyName("link_header")]
    public string LinkHeader { get; init; }
        = null;

    [JsonPropertyName("pushed_at")]
    public string PushedAt { get; init; }
        = null;

    [JsonPropertyName("owner_type")]
    public string OwnerType { get; init; }
        = null;

    [JsonPropertyName("main_branch")]
    public string MainBranch { get; init; }
        = null;
}

public record class BranchDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("commit_sha")]
    public string CommitSha { get; init; }

    [JsonPropertyName("protected")]
    public bool Protected { get; init; }
        = false;

    [JsonPropertyName("last_push_date")]
    public string LastPushDate { get; init; }
        = null;
}

public record class PaginatedBranchesResponseDto
{
    [JsonPropertyName("branches")]
    public IReadOnlyList<BranchDto> Branches { get; init; }
        = Array.Empty<BranchDto>();

    [JsonPropertyName("has_next_page")]
    public bool HasNextPage { get; init; }
        = false;

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; init; }
        = 1;

    [JsonPropertyName("per_page")]
    public int PerPage { get; init; }
        = 30;

    [JsonPropertyName("total_count")]
    public int? TotalCount { get; init; }
        = null;
}

public record class RepositoryMicroagentDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; init; } = "github";

    [JsonPropertyName("path")]
    public string Path { get; init; }
}

public record class MicroagentContentResponseDto
{
    [JsonPropertyName("content")]
    public string Content { get; init; }

    [JsonPropertyName("path")]
    public string Path { get; init; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; init; } = "github";

    [JsonPropertyName("triggers")]
    public IReadOnlyList<string> Triggers { get; init; }
        = Array.Empty<string>();
}

public record class GitChangeDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("path")]
    public string Path { get; init; }
}

public record class GitChangeDiffDto
{
    [JsonPropertyName("modified")]
    public string Modified { get; init; }

    [JsonPropertyName("original")]
    public string Original { get; init; }
}

public record class SuggestedTaskDto
{
    [JsonPropertyName("git_provider")]
    public string GitProvider { get; init; } = "github";

    [JsonPropertyName("issue_number")]
    public int IssueNumber { get; init; }
        = 0;

    [JsonPropertyName("repo")]
    public string Repository { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("task_type")]
    public string TaskType { get; init; } = "OPEN_ISSUE";
}
