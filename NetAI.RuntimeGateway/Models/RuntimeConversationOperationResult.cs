using System.Text.Json.Serialization;

namespace NetAI.RuntimeGateway.Models
{
    public sealed class RuntimeConversationOperationResult
    {
        [JsonPropertyName("status")]
        public string Status { get; init; }

        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; init; }

        [JsonPropertyName("conversation_status")]
        public string ConversationStatus { get; init; }

        [JsonPropertyName("runtime_status")]
        public string RuntimeStatus { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; }

        [JsonPropertyName("session_api_key")]
        public string SessionApiKey { get; init; }

        [JsonPropertyName("runtime_id")]
        public string RuntimeId { get; init; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; init; }
    }
}
