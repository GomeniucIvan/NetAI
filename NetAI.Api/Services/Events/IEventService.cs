using NetAI.Api.Models.Events;
using NetAI.Api.Models.Webhooks;

namespace NetAI.Api.Services.Events;

public interface IEventService
{
    Task<EventPageDto> SearchEventsAsync(
        string conversationIdEquals,
        string kindEquals,
        DateTimeOffset? timestampGreaterThanOrEqual,
        DateTimeOffset? timestampLessThan,
        EventSortOrder sortOrder,
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task<int> CountEventsAsync(
        string conversationIdEquals,
        string kindEquals,
        DateTimeOffset? timestampGreaterThanOrEqual,
        DateTimeOffset? timestampLessThan,
        EventSortOrder sortOrder,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EventDto>> BatchGetEventsAsync(
        IReadOnlyList<string> eventIds,
        CancellationToken cancellationToken);

    Task SaveEventAsync(
        string conversationId,
        WebhookEventDto webhookEvent,
        CancellationToken cancellationToken);

    Task SaveEventsAsync(
        string conversationId,
        IReadOnlyList<WebhookEventDto> events,
        CancellationToken cancellationToken);
}
