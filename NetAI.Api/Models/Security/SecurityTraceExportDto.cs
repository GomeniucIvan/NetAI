using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Security;

public class SecurityTraceExportDto
{
    [JsonPropertyName("exported_at")]
    public DateTimeOffset ExportedAt { get; set; }

    [JsonPropertyName("policy")]
    public string Policy { get; set; }

    [JsonPropertyName("risk_severity")]
    public int RiskSeverity { get; set; }

    [JsonPropertyName("conversations")]
    public IReadOnlyList<SecurityTraceConversationDto> Conversations { get; set; } = Array.Empty<SecurityTraceConversationDto>();
}

public class SecurityTraceConversationDto
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("events")]
    public IReadOnlyList<SecurityTraceEventDto> Events { get; set; } = Array.Empty<SecurityTraceEventDto>();
}

public class SecurityTraceEventDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("event")]
    public JsonElement Event { get; set; }
}
