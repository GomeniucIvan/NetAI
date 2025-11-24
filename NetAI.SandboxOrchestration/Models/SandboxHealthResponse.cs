using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class SandboxHealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; init; }

    [JsonPropertyName("build_status")]
    public string BuildStatus { get; init; }

    [JsonPropertyName("checked_at")]
    public DateTimeOffset CheckedAt { get; init; }

    [JsonPropertyName("details")]
    public string Details { get; init; }
}
