using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationRememberPromptRecord
{
    public Guid Id { get; set; }

    public int EventId { get; set; }

    public string Prompt { get; set; }

    public Guid ConversationMetadataRecordId { get; set; }

    public ConversationMetadataRecord Conversation { get; set; } = default!;
}
