using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class RuntimeSandboxDetails
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; init; } 

    [JsonPropertyName("url")]
    public string Url { get; init; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; init; }

    [JsonPropertyName("working_dir")]
    public string WorkingDirectory { get; init; } 

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("sandbox_spec_id")]
    public string SandboxSpecId { get; init; }
}
