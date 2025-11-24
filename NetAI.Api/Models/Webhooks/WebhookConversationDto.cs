using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Webhooks;

public class WebhookConversationDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("created_by_user_id")]
    public string CreatedByUserId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("selected_repository")]
    public string SelectedRepository { get; set; }

    [JsonPropertyName("selected_branch")]
    public string SelectedBranch { get; set; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; set; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; }

    [JsonPropertyName("pr_number")]
    public IReadOnlyList<int> PullRequestNumbers { get; set; }

    [JsonPropertyName("agent")]
    public WebhookConversationAgentDto Agent { get; set; }
}

public class WebhookConversationAgentDto
{
    [JsonPropertyName("llm")]
    public WebhookConversationLlmDto Llm { get; set; }
}

public class WebhookConversationLlmDto
{
    [JsonPropertyName("model")]
    public string Model { get; set; }
}
