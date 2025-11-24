using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationEventRecord
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int EventId { get; set; }

    [MaxLength(64)]
    public string Type { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    [Required]
    public string PayloadJson { get; set; }

    public Guid ConversationMetadataRecordId { get; set; }

    public ConversationMetadataRecord Conversation { get; set; } = default!;
}
