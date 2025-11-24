using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationRuntimeHostRecord
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(512)]
    public string Url { get; set; }

    public Guid ConversationRuntimeInstanceRecordId { get; set; }

    public ConversationRuntimeInstanceRecord RuntimeInstance { get; set; } = default!;
}
