using System;

namespace NetAI.RuntimeGateway.Models;

public sealed class RuntimeConversationEventAppendedEventArgs : EventArgs
{
    public RuntimeConversationEventAppendedEventArgs(string conversationId, RuntimeConversationEvent conversationEvent)
    {
        ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
        Event = conversationEvent ?? throw new ArgumentNullException(nameof(conversationEvent));
    }

    public string ConversationId { get; }

    public RuntimeConversationEvent Event { get; }
}
