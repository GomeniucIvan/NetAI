using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationRuntimeProviderRecord
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string Provider { get; set; } 

    public Guid ConversationRuntimeInstanceRecordId { get; set; }

    public ConversationRuntimeInstanceRecord RuntimeInstance { get; set; } = default!;
}
