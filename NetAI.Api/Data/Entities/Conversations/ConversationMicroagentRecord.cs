using System.ComponentModel.DataAnnotations;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationMicroagentRecord
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    [MaxLength(50)]
    public string Type { get; set; }

    public string Content { get; set; }

    public string TriggersJson { get; set; }

    public string InputsJson { get; set; }

    public string ToolsJson { get; set; }

    public Guid ConversationMetadataRecordId { get; set; }

    public ConversationMetadataRecord Conversation { get; set; } = default!;
}
