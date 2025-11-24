using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class SandboxSpecInfoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } 

    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("initial_env")]
    public IReadOnlyDictionary<string, string> InitialEnv { get; set; } = new Dictionary<string, string>();

    [JsonPropertyName("working_dir")]
    public string WorkingDir { get; set; }
}
