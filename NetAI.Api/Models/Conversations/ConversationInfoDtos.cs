using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class ConversationInfoDto
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("last_updated_at")]
    public DateTimeOffset? LastUpdatedAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; set; }

    [JsonPropertyName("selected_repository")]
    public string SelectedRepository { get; set; }

    [JsonPropertyName("selected_branch")]
    public string SelectedBranch { get; set; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; set; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; }

    [JsonPropertyName("num_connections")]
    public int NumConnections { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("pr_number")]
    public IReadOnlyList<int> PullRequestNumbers { get; set; }

    [JsonPropertyName("conversation_version")]
    public string ConversationVersion { get; set; }
}

public class ConversationInfoResultSetDto
{
    [JsonPropertyName("results")]
    public IReadOnlyList<ConversationInfoDto> Results { get; set; } = Array.Empty<ConversationInfoDto>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
