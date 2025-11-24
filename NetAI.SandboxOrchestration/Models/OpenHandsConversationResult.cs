using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class OpenHandsConversationResult
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; init; } 

    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; init; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; init; }

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; init; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; }

    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; init; }
}
