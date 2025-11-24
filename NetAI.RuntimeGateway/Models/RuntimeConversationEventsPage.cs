using System.Collections.Generic;

namespace NetAI.RuntimeGateway.Models;

public sealed class RuntimeConversationEventsPage
{
    public RuntimeConversationEventsPage(IReadOnlyList<RuntimeConversationEvent> events, bool hasMore)
    {
        Events = events;
        HasMore = hasMore;
    }

    public IReadOnlyList<RuntimeConversationEvent> Events { get; }

    public bool HasMore { get; }
}
