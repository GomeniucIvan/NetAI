using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Events;

public class EventDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } 

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
