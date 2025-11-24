using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationFeedbackRecord
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string FeedbackJson { get; set; }

    public Guid ConversationMetadataRecordId { get; set; }

    public ConversationMetadataRecord Conversation { get; set; } = default!;
}
