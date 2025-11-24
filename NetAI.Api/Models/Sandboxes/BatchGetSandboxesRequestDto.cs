using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class BatchGetSandboxesRequestDto
{
    [Required]
    [MinLength(1)]
    [JsonPropertyName("sandbox_ids")]
    public IReadOnlyList<string> SandboxIds { get; set; } = Array.Empty<string>();
}
