using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;

namespace NetAI.Api.Services.EventCallbacks;

public class EventCallbackManagementService : IEventCallbackManagementService
{
    private readonly IEventCallbackRepository _callbackRepository;
    private readonly IEventCallbackResultRepository _resultRepository;
    public EventCallbackManagementService(
        IEventCallbackRepository callbackRepository,
        IEventCallbackResultRepository resultRepository,
        ILogger<EventCallbackManagementService> logger)
    {
        _callbackRepository = callbackRepository ?? throw new ArgumentNullException(nameof(callbackRepository));
        _resultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EventCallbackDto> CreateCallbackAsync(CreateEventCallbackRequestDto request, CancellationToken cancellationToken)
    {
        ValidateProcessor(request.Processor);

        EventCallbackRecord record = new()
        {
            ConversationId = request.ConversationId,
            EventKind = string.IsNullOrWhiteSpace(request.EventKind) ? null : request.EventKind!.Trim(),
            ProcessorJson = JsonSerializer.Serialize(request.Processor)
        };

        EventCallbackRecord created = await _callbackRepository
            .CreateAsync(record, cancellationToken)
            .ConfigureAwait(false);

        return MapCallback(created);
    }

    public async Task<EventCallbackDto> GetCallbackAsync(Guid id, CancellationToken cancellationToken)
    {
        EventCallbackRecord record = await _callbackRepository
            .GetAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : MapCallback(record);
    }

    public async Task<bool> DeleteCallbackAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _callbackRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EventCallbackPageDto> SearchCallbacksAsync(SearchEventCallbacksRequest request, CancellationToken cancellationToken)
    {
        (IReadOnlyList<EventCallbackRecord> Items, string NextPageId) result = await _callbackRepository
            .SearchAsync(request.ConversationId, request.EventKind, request.PageId, request.Limit, cancellationToken)
            .ConfigureAwait(false);

        return new EventCallbackPageDto
        {
            Items = result.Items.Select(MapCallback).ToList(),
            NextPageId = result.NextPageId
        };
    }

    public async Task<IReadOnlyList<EventCallbackDto>> BatchGetCallbacksAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        IReadOnlyList<EventCallbackRecord> records = await _callbackRepository
            .BatchGetAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        return records.Select(MapCallback).ToList();
    }

    public async Task<EventCallbackResultDto> CreateResultAsync(CreateEventCallbackResultRequestDto request, CancellationToken cancellationToken)
    {
        EventCallbackResultRecord record = new()
        {
            Status = request.Status,
            EventCallbackId = request.EventCallbackId,
            EventId = request.EventId,
            ConversationId = request.ConversationId,
            Detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail!.Trim()
        };

        EventCallbackResultRecord created = await _resultRepository
            .CreateAsync(record, cancellationToken)
            .ConfigureAwait(false);

        return MapResult(created);
    }

    public async Task<EventCallbackResultDto> GetResultAsync(Guid id, CancellationToken cancellationToken)
    {
        EventCallbackResultRecord record = await _resultRepository
            .GetAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : MapResult(record);
    }

    public async Task<bool> DeleteResultAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _resultRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EventCallbackResultPageDto> SearchResultsAsync(SearchEventCallbackResultsRequest request, CancellationToken cancellationToken)
    {
        (IReadOnlyList<EventCallbackResultRecord> Items, string NextPageId) result = await _resultRepository
            .SearchAsync(
                request.ConversationId,
                request.EventCallbackId,
                request.EventId,
                request.Status,
                request.SortOrder,
                request.PageId,
                request.Limit,
                cancellationToken)
            .ConfigureAwait(false);

        return new EventCallbackResultPageDto
        {
            Items = result.Items.Select(MapResult).ToList(),
            NextPageId = result.NextPageId
        };
    }

    public async Task<IReadOnlyList<EventCallbackResultDto>> BatchGetResultsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        IReadOnlyList<EventCallbackResultRecord> records = await _resultRepository
            .BatchGetAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        return records.Select(MapResult).ToList();
    }

    private static void ValidateProcessor(JsonElement processor)
    {
        if (processor.ValueKind == JsonValueKind.Undefined || processor.ValueKind == JsonValueKind.Null)
        {
            throw new ValidationException("Processor payload is required.");
        }
    }

    private static EventCallbackDto MapCallback(EventCallbackRecord record)
    {
        return new EventCallbackDto
        {
            Id = record.Id,
            ConversationId = record.ConversationId,
            EventKind = record.EventKind,
            Processor = ParseJson(record.ProcessorJson),
            CreatedAtUtc = record.CreatedAtUtc
        };
    }

    private static EventCallbackResultDto MapResult(EventCallbackResultRecord record)
    {
        return new EventCallbackResultDto
        {
            Id = record.Id,
            Status = record.Status,
            EventCallbackId = record.EventCallbackId,
            EventId = record.EventId,
            ConversationId = record.ConversationId,
            Detail = record.Detail,
            CreatedAtUtc = record.CreatedAtUtc
        };
    }

    private static JsonElement ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            using JsonDocument emptyDoc = JsonDocument.Parse("{}");
            return emptyDoc.RootElement.Clone();
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
