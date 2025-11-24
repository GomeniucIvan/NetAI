using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.Conversations;

/// <summary>
/// Represents an attached runtime conversation session.
/// </summary>
public sealed class RuntimeConversationHandle
{
    public RuntimeConversationHandle(ConversationMetadataRecord conversation)
    {
        Conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
    }

    /// <summary>
    /// Gets the backing conversation metadata record.
    /// </summary>
    public ConversationMetadataRecord Conversation { get; }
}
