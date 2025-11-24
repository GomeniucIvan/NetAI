using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class SandboxLifecycleActionResponse
{
    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; init; }

    [JsonPropertyName("action")]
    public string Action { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; }

    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; init; }
}
