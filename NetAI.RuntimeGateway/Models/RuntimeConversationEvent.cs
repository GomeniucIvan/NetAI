using System;
using System.Text.Json;

namespace NetAI.RuntimeGateway.Models;

public sealed class RuntimeConversationEvent
{
    public int EventId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string Type { get; init; } = "event";

    public string PayloadJson { get; init; } = "{}";

    public JsonElement GetPayload()
    {
        using JsonDocument document = JsonDocument.Parse(PayloadJson);
        return document.RootElement.Clone();
    }
}
