using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class RuntimeStartRequest
{
    [JsonPropertyName("build_id")]
    public string BuildId { get; set; } = string.Empty;

    [JsonPropertyName("sandbox_spec_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SandboxSpecId { get; set; }

    [JsonPropertyName("sandbox_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SandboxId { get; set; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string> Metadata { get; set; }

    [JsonPropertyName("environment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string> Environment { get; set; }
}
