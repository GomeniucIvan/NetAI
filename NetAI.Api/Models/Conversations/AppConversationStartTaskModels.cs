using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class AppConversationStartRequestDto
{
    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("initial_message")]
    public AppConversationMessageDto InitialMessage { get; set; }

    [JsonPropertyName("processors")]
    public JsonElement? Processors { get; set; }

    [JsonPropertyName("llm_model")]
    public string LlmModel { get; set; }

    [JsonPropertyName("selected_repository")]
    public string SelectedRepository { get; set; }

    [JsonPropertyName("selected_branch")]
    public string SelectedBranch { get; set; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; }

    [JsonPropertyName("pr_number")]
    public IList<int> PullRequestNumbers { get; set; }

    [JsonPropertyName("created_by_user_id")]
    public string CreatedByUserId { get; set; }
}

public class AppConversationMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public IList<AppConversationMessageContentDto> Content { get; set; }
}

public class AppConversationMessageContentDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("image_url")]
    public AppConversationImageContentDto ImageUrl { get; set; }
}

public class AppConversationImageContentDto
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class AppConversationStartTaskDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("created_by_user_id")]
    public string CreatedByUserId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "WORKING";

    [JsonPropertyName("detail")]
    public string Detail { get; set; }

    [JsonPropertyName("failure_detail")]
    public string FailureDetail { get; set; }

    [JsonPropertyName("app_conversation_id")]
    public string AppConversationId { get; set; }

    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("agent_server_url")]
    public string AgentServerUrl { get; set; }

    [JsonPropertyName("sandbox_session_api_key")]
    public string SandboxSessionApiKey { get; set; }

    [JsonPropertyName("sandbox_workspace_path")]
    public string SandboxWorkspacePath { get; set; }

    [JsonPropertyName("sandbox_vscode_url")]
    public string SandboxVscodeUrl { get; set; }

    [JsonPropertyName("conversation_status")]
    public string ConversationStatus { get; set; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; set; }

    [JsonPropertyName("error")]
    public string BackendError { get; set; }

    [JsonPropertyName("request")]
    public AppConversationStartRequestDto Request { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AppConversationStartTaskPageDto
{
    [JsonPropertyName("items")]
    public IReadOnlyList<AppConversationStartTaskDto> Items { get; set; } = Array.Empty<AppConversationStartTaskDto>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
