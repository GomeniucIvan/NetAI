using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class ConversationEventsPageDto
{
    [JsonPropertyName("events")]
    public IReadOnlyList<ConversationEventDto> Events { get; set; } = Array.Empty<ConversationEventDto>();

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
