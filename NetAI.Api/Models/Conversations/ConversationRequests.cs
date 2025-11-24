using System.Text.Json.Serialization;
using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Models.Conversations;

public class CreateConversationRequestDto
{
    [JsonPropertyName("repository")]
    public string Repository { get; set; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; set; }

    [JsonPropertyName("selected_branch")]
    public string SelectedBranch { get; set; }

    [JsonPropertyName("initial_user_msg")]
    public string InitialUserMessage { get; set; }

    [JsonPropertyName("suggested_task")]
    public SuggestedTaskDto SuggestedTask { get; set; }

    [JsonPropertyName("conversation_instructions")]
    public string ConversationInstructions { get; set; }

    [JsonPropertyName("create_microagent")]
    public CreateMicroagentDto CreateMicroagent { get; set; }

    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("sandbox_connection")]
    public SandboxConnectionInfoDto SandboxConnection { get; set; }

    [JsonPropertyName("pr_number")]
    public IList<int> PullRequestNumbers { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}

public class SuggestedTaskDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }
}

public class CreateMicroagentDto
{
    [JsonPropertyName("repo")]
    public string Repo { get; set; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}

public class UpdateConversationRequestDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; }
}

public class ConversationStartRequestDto
{
    [JsonPropertyName("providers_set")]
    public IList<string> ProvidersSet { get; set; }
}

public class SandboxConnectionInfoDto
{
    [JsonPropertyName("agent_server_url")]
    public string AgentServerUrl { get; set; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }

    [JsonPropertyName("vscode_url")]
    public string VscodeUrl { get; set; }

    [JsonPropertyName("workspace_path")]
    public string WorkspacePath { get; set; }

    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; set; }

    [JsonPropertyName("runtime_hosts")]
    public IReadOnlyList<SandboxRuntimeHostDto> RuntimeHosts { get; set; }
}

public class ConversationMessageRequestDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
