using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class ConversationResponseDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("conversation_status")]
    public string ConversationStatus { get; set; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}
