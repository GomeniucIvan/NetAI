using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class EventCallbackRecord
{
    public Guid Id { get; set; }

    public Guid? ConversationId { get; set; }

    [Required]
    public string ProcessorJson { get; set; } = string.Empty;

    [MaxLength(200)]
    public string EventKind { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<EventCallbackResultRecord> Results { get; set; } = new List<EventCallbackResultRecord>();
}
