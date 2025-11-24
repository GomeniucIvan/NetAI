using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class SandboxSpecInfoPageDto
{
    [JsonPropertyName("items")]
    public IReadOnlyList<SandboxSpecInfoDto> Items { get; set; } = Array.Empty<SandboxSpecInfoDto>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
