using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class SandboxPageDto
{
    [JsonPropertyName("items")]
    public IReadOnlyList<SandboxInfoDto> Items { get; set; } = Array.Empty<SandboxInfoDto>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
