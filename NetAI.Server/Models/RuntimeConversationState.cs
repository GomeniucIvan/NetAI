namespace NetAI.Server.Models;

public sealed class RuntimeConversationState
{
    public required string ConversationId { get; init; }

    public string ConversationStatus { get; set; } = "ready";

    public string RuntimeStatus { get; set; } = "ready";

    public List<RuntimeConversationEvent> Events { get; } = new();

    public string? WorkspacePath { get; set; }

    public string SessionApiKey { get; set; } = string.Empty;

    public string? RuntimeId { get; set; }

    public string? SessionId { get; set; }
}
