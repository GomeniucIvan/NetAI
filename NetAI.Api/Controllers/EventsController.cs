using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Events;
using NetAI.Api.Services.Events;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<EventPageDto>> SearchAsync(
        [FromQuery(Name = "conversation_id__eq")] string conversationIdEquals,
        [FromQuery(Name = "kind__eq")] string kindEquals,
        [FromQuery(Name = "timestamp__gte")] DateTimeOffset? timestampGreaterThanOrEqual,
        [FromQuery(Name = "timestamp__lt")] DateTimeOffset? timestampLessThan,
        [FromQuery(Name = "sort_order")] EventSortOrder sortOrder = EventSortOrder.Timestamp,
        [FromQuery(Name = "page_id")] string pageId = null,
        [FromQuery(Name = "limit")] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EventPageDto result = await _eventService
                .SearchEventsAsync(
                    conversationIdEquals,
                    kindEquals,
                    timestampGreaterThanOrEqual,
                    timestampLessThan,
                    sortOrder,
                    pageId,
                    limit,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex) when (string.Equals(ex.ParamName, "pageId", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> CountAsync(
        [FromQuery(Name = "conversation_id__eq")] string conversationIdEquals,
        [FromQuery(Name = "kind__eq")] string kindEquals,
        [FromQuery(Name = "timestamp__gte")] DateTimeOffset? timestampGreaterThanOrEqual,
        [FromQuery(Name = "timestamp__lt")] DateTimeOffset? timestampLessThan,
        [FromQuery(Name = "sort_order")] EventSortOrder sortOrder = EventSortOrder.Timestamp,
        CancellationToken cancellationToken = default)
    {
        int count = await _eventService
            .CountEventsAsync(
                conversationIdEquals,
                kindEquals,
                timestampGreaterThanOrEqual,
                timestampLessThan,
                sortOrder,
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(count);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventDto>>> BatchGetAsync(
        [FromQuery(Name = "id")] List<string> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count > 100)
        {
            return BadRequest(new { error = "A maximum of 100 event identifiers may be requested per call." });
        }

        IReadOnlyList<EventDto> events = await _eventService
            .BatchGetEventsAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        return Ok(events);
    }
}
