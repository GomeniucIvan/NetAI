using System.ComponentModel.DataAnnotations;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Entities.Conversations;

public class ConversationRuntimeInstanceRecord
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string RuntimeId { get; set; }

    [MaxLength(200)]
    public string SessionId { get; set; }

    [MaxLength(200)]
    public string SessionApiKey { get; set; }

    [MaxLength(100)]
    public string RuntimeStatus { get; set; }

    [MaxLength(100)]
    public string Status { get; set; } = "STARTING"; //TODO review enum

    [MaxLength(512)]
    public string VscodeUrl { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid ConversationMetadataRecordId { get; set; }

    public ConversationMetadataRecord Conversation { get; set; } = default!;

    public ICollection<ConversationRuntimeHostRecord> Hosts { get; set; } = new List<ConversationRuntimeHostRecord>();

    public ICollection<ConversationRuntimeProviderRecord> Providers { get; set; } = new List<ConversationRuntimeProviderRecord>();
}
