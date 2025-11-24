using System.Text.Json.Serialization;

namespace NetAI.Api.Models.ConversationRuntime;

public class ConversationRuntimeInfoDto
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }
}
