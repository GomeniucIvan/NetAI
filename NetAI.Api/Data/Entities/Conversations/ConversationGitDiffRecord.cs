using System.ComponentModel.DataAnnotations;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationGitDiffRecord
{
    public Guid Id { get; set; }

    [MaxLength(512)]
    public string Path { get; set; }

    public string Original { get; set; }

    public string Modified { get; set; }

    public Guid ConversationMetadataRecordId { get; set; }

    public ConversationMetadataRecord Conversation { get; set; } = default!;
}
