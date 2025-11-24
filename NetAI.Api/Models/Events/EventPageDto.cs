using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Events;

public class EventPageDto
{
    [JsonPropertyName("items")]
    public IReadOnlyList<EventDto> Items { get; set; } = Array.Empty<EventDto>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
