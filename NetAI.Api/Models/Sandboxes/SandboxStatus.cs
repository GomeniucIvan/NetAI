using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SandboxStatus
{
    STARTING,
    RUNNING,
    PAUSED,
    ERROR,
    MISSING,
}
