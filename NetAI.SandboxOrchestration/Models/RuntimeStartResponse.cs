using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class RuntimeStartResponse
{
    [JsonPropertyName("build_id")]
    public string BuildId { get; set; }

    [JsonPropertyName("runtime")]
    public RuntimeSandboxDetails Runtime { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; set; }
}
