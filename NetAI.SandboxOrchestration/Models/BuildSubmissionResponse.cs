using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class BuildSubmissionResponse
{
    [JsonPropertyName("build_id")]
    public string BuildId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; init; }
}
