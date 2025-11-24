using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Webhooks;

public class WebhookEventDto
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
