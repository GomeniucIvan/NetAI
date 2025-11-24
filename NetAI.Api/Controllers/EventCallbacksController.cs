using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Services.EventCallbacks;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/v1/event-callbacks")]
public class EventCallbacksController : ControllerBase
{
    private readonly IEventCallbackManagementService _service;

    public EventCallbacksController(
        IEventCallbackManagementService service,
        ILogger<EventCallbacksController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<ActionResult<EventCallbackPageDto>> ListCallbacks(
        [FromQuery(Name = "conversation_id__eq")] Guid? conversationId,
        [FromQuery(Name = "event_kind__eq")] string eventKind,
        [FromQuery(Name = "page_id")] string pageId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchEventCallbacksRequest
        {
            ConversationId = conversationId,
            EventKind = eventKind,
            PageId = pageId,
            Limit = limit
        };

        EventCallbackPageDto result = await _service
            .SearchCallbacksAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EventCallbackDto>> CreateCallback(
        [FromBody] CreateEventCallbackRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EventCallbackDto created = await _service
                .CreateCallbackAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return CreatedAtAction(nameof(GetCallback), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventCallbackDto>> GetCallback(Guid id, CancellationToken cancellationToken = default)
    {
        EventCallbackDto callback = await _service
            .GetCallbackAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (callback is null)
        {
            return NotFound();
        }

        return Ok(callback);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCallback(Guid id, CancellationToken cancellationToken = default)
    {
        bool deleted = await _service
            .DeleteCallbackAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("batch")]
    public async Task<ActionResult<BatchGetEventCallbacksResponseDto>> BatchGetCallbacks(
        [FromBody] BatchGetEventCallbacksRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
        {
            return BadRequest(new { error = "At least one callback id must be provided." });
        }

        IReadOnlyList<EventCallbackDto> items = await _service
            .BatchGetCallbacksAsync(request.Ids, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new BatchGetEventCallbacksResponseDto { Items = items });
    }

    [HttpGet("results")]
    public async Task<ActionResult<EventCallbackResultPageDto>> ListResults(
        [FromQuery(Name = "conversation_id__eq")] Guid? conversationId,
        [FromQuery(Name = "event_callback_id__eq")] Guid? eventCallbackId,
        [FromQuery(Name = "event_id__eq")] Guid? eventId,
        [FromQuery(Name = "status__eq")] string status,
        [FromQuery(Name = "sort_order")] EventCallbackResultSortOrder sortOrder = EventCallbackResultSortOrder.CreatedAtDesc,
        [FromQuery(Name = "page_id")] string pageId = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        EventCallbackResultStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<EventCallbackResultStatus>(status, ignoreCase: true, out EventCallbackResultStatus parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var request = new SearchEventCallbackResultsRequest
        {
            ConversationId = conversationId,
            EventCallbackId = eventCallbackId,
            EventId = eventId,
            Status = statusFilter,
            SortOrder = sortOrder,
            PageId = pageId,
            Limit = limit
        };

        EventCallbackResultPageDto result = await _service
            .SearchResultsAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("results")]
    public async Task<ActionResult<EventCallbackResultDto>> CreateResult(
        [FromBody] CreateEventCallbackResultRequestDto request,
        CancellationToken cancellationToken = default)
    {
        EventCallbackResultDto created = await _service
            .CreateResultAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return CreatedAtAction(nameof(GetResult), new { id = created.Id }, created);
    }

    [HttpGet("results/{id:guid}")]
    public async Task<ActionResult<EventCallbackResultDto>> GetResult(Guid id, CancellationToken cancellationToken = default)
    {
        EventCallbackResultDto result = await _service
            .GetResultAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpDelete("results/{id:guid}")]
    public async Task<IActionResult> DeleteResult(Guid id, CancellationToken cancellationToken = default)
    {
        bool deleted = await _service
            .DeleteResultAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("results/batch")]
    public async Task<ActionResult<BatchGetEventCallbackResultsResponseDto>> BatchGetResults(
        [FromBody] BatchGetEventCallbackResultsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
        {
            return BadRequest(new { error = "At least one result id must be provided." });
        }

        IReadOnlyList<EventCallbackResultDto> items = await _service
            .BatchGetResultsAsync(request.Ids, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new BatchGetEventCallbackResultsResponseDto { Items = items });
    }
}
