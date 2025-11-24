using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class ExposedUrlDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}
