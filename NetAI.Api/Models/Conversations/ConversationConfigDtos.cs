using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class RuntimeConfigResponseDto
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; }
}

public class VSCodeUrlResponseDto
{
    [JsonPropertyName("vscode_url")]
    public string VscodeUrl { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }
}

public class WebHostsResponseDto
{
    [JsonPropertyName("hosts")]
    public IDictionary<string, string> Hosts { get; set; } = new Dictionary<string, string>();
}

public class TrajectoryResponseDto
{
    [JsonPropertyName("trajectory")]
    public IReadOnlyList<JsonElement> Trajectory { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }
}
