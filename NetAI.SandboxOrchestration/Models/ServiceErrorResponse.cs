using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class ServiceErrorResponse
{
    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, object> Details { get; set; }
}
