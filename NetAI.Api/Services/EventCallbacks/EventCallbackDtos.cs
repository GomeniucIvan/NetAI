using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.EventCallbacks;

public record CreateEventCallbackRequestDto
{
    public Guid? ConversationId { get; init; }

    [Required]
    public JsonElement Processor { get; init; }

    public string EventKind { get; init; }
}

public record EventCallbackDto
{
    public Guid Id { get; init; }

    public Guid? ConversationId { get; init; }

    public JsonElement Processor { get; init; }

    public string EventKind { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public record EventCallbackPageDto
{
    public IReadOnlyList<EventCallbackDto> Items { get; init; } = Array.Empty<EventCallbackDto>();

    public string NextPageId { get; init; }
}

public record BatchGetEventCallbacksRequestDto
{
    [Required]
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
}

public record BatchGetEventCallbacksResponseDto
{
    public IReadOnlyList<EventCallbackDto> Items { get; init; } = Array.Empty<EventCallbackDto>();
}

public record CreateEventCallbackResultRequestDto
{
    [Required]
    public EventCallbackResultStatus Status { get; init; }

    [Required]
    public Guid EventCallbackId { get; init; }

    [Required]
    public Guid EventId { get; init; }

    [Required]
    public Guid ConversationId { get; init; }

    public string Detail { get; init; }
}

public record EventCallbackResultDto
{
    public Guid Id { get; init; }

    public EventCallbackResultStatus Status { get; init; }

    public Guid EventCallbackId { get; init; }

    public Guid EventId { get; init; }

    public Guid ConversationId { get; init; }

    public string Detail { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public record EventCallbackResultPageDto
{
    public IReadOnlyList<EventCallbackResultDto> Items { get; init; } = Array.Empty<EventCallbackResultDto>();

    public string NextPageId { get; init; }
}

public record BatchGetEventCallbackResultsRequestDto
{
    [Required]
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
}

public record BatchGetEventCallbackResultsResponseDto
{
    public IReadOnlyList<EventCallbackResultDto> Items { get; init; } = Array.Empty<EventCallbackResultDto>();
}

public record SearchEventCallbacksRequest
{
    public Guid? ConversationId { get; init; }

    public string EventKind { get; init; }

    public string PageId { get; init; }

    public int Limit { get; init; } = 100;
}

public record SearchEventCallbackResultsRequest
{
    public Guid? ConversationId { get; init; }

    public Guid? EventCallbackId { get; init; }

    public Guid? EventId { get; init; }

    public EventCallbackResultStatus? Status { get; init; }

    public EventCallbackResultSortOrder SortOrder { get; init; } = EventCallbackResultSortOrder.CreatedAtDesc;

    public string PageId { get; init; }

    public int Limit { get; init; } = 100;
}
