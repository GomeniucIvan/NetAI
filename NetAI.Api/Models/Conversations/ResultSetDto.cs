using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

//todo shared
public class ResultSetDto<T>
{
    [JsonPropertyName("results")]
    public IReadOnlyList<T> Results { get; set; } = Array.Empty<T>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
