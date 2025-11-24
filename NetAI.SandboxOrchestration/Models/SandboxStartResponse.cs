using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class SandboxStartResponse
{
    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } 

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; set; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }

    [JsonPropertyName("workspace_path")]
    public string WorkspacePath { get; set; }

    [JsonPropertyName("sandbox_spec_id")]
    public string SandboxSpecId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; set; }
}
