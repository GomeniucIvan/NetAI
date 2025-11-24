using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class EventCallbackResultRecord
{
    public Guid Id { get; set; }

    public EventCallbackResultStatus Status { get; set; }

    public Guid EventCallbackId { get; set; }

    public Guid EventId { get; set; }

    public Guid ConversationId { get; set; }

    [MaxLength(4000)]
    public string Detail { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public EventCallbackRecord EventCallback { get; set; }
}

public enum EventCallbackResultStatus
{
    Success,
    Error
}

public enum EventCallbackResultSortOrder
{
    CreatedAt,
    CreatedAtDesc
}
