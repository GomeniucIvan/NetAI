using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class StartSandboxRequestDto
{
    [JsonPropertyName("sandbox_spec_id")]
    public string SandboxSpecId { get; set; }

    [JsonPropertyName("created_by_user_id")]
    public string CreatedByUserId { get; set; }
}
