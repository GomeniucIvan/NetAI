using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class ConversationEventDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("event")]
    public JsonElement Event { get; set; }
}
