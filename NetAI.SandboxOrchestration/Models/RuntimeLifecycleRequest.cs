using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class RuntimeLifecycleRequest
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }
}
