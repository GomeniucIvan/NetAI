using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class BatchGetSandboxSpecsRequestDto
{
    [Required]
    [MinLength(1)]
    [JsonPropertyName("sandbox_spec_ids")]
    public IReadOnlyList<string> SandboxSpecIds { get; set; } = Array.Empty<string>();
}
