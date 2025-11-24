using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class RuntimeStatusResponse
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("phase")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Phase { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, object> Details { get; init; }
}
