using System.Text.Json;

namespace NetAI.Server.Models;

public sealed class RuntimeConversationEvent
{
    public required int Id { get; init; }

    public required string Type { get; init; }

    public JsonElement Payload { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
