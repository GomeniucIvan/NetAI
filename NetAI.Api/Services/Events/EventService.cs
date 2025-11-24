using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data;
using NetAI.Api.Data.Entities.Conversations;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Events;
using NetAI.Api.Models.Webhooks;
using NetAI.Api.Services.WebSockets;

namespace NetAI.Api.Services.Events;

public class EventService : IEventService
{
    private const int MaxPageSize = 100;
    private const char EventIdSeparator = ':';

    private readonly NetAiDbContext _dbContext;
    private readonly IConversationEventNotifier _eventNotifier;

    public EventService(NetAiDbContext dbContext, IConversationEventNotifier eventNotifier)
    {
        _dbContext = dbContext;
        _eventNotifier = eventNotifier;
    }

    public Task SaveEventAsync(
        string conversationId,
        WebhookEventDto webhookEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        return SaveEventsAsync(conversationId, new[] { webhookEvent }, cancellationToken);
    }

    public async Task SaveEventsAsync(
        string conversationId,
        IReadOnlyList<WebhookEventDto> events,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("Conversation identifier is required.", nameof(conversationId));
        }

        if (events.Count == 0)
        {
            return;
        }

        ConversationMetadataRecord conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(record => record.ConversationId == conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            throw new InvalidOperationException($"Conversation '{conversationId}' not found.");
        }

        HashSet<int> providedIdentifiers = events
            .Where(evt => evt?.Id is int value && value >= 0)
            .Select(evt => evt!.Id!.Value)
            .ToHashSet();

        Dictionary<int, ConversationEventRecord> existing = providedIdentifiers.Count == 0
            ? new Dictionary<int, ConversationEventRecord>()
            : await _dbContext.ConversationEvents
                .Where(evt => evt.ConversationMetadataRecordId == conversation.Id && providedIdentifiers.Contains(evt.EventId))
                .ToDictionaryAsync(evt => evt.EventId, cancellationToken)
                .ConfigureAwait(false);

        int nextEventId = await _dbContext.ConversationEvents
            .Where(evt => evt.ConversationMetadataRecordId == conversation.Id)
            .MaxAsync(evt => (int?)evt.EventId, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        DateTimeOffset defaultTimestamp = DateTimeOffset.UtcNow;

        var pendingPublishes = new List<(int EventId, string Payload)>();

        foreach (WebhookEventDto webhookEvent in events)
        {
            if (webhookEvent is null || string.IsNullOrWhiteSpace(webhookEvent.Kind))
            {
                continue;
            }

            int eventId = webhookEvent.Id is int providedId && providedId >= 0
                ? providedId
                : ++nextEventId;

            DateTimeOffset timestamp = webhookEvent.Timestamp ?? defaultTimestamp;

            if (existing.TryGetValue(eventId, out ConversationEventRecord record))
            {
                record.Type = webhookEvent.Kind;
                record.CreatedAtUtc = timestamp;
                record.PayloadJson = SerializePayload(webhookEvent.Payload);
                pendingPublishes.Add((eventId, record.PayloadJson));
                continue;
            }

            var newRecord = new ConversationEventRecord
            {
                ConversationMetadataRecordId = conversation.Id,
                EventId = eventId,
                Type = webhookEvent.Kind,
                CreatedAtUtc = timestamp,
                PayloadJson = SerializePayload(webhookEvent.Payload)
            };

            await _dbContext.ConversationEvents.AddAsync(newRecord, cancellationToken).ConfigureAwait(false);
            pendingPublishes.Add((eventId, newRecord.PayloadJson));
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (pendingPublishes.Count == 0)
        {
            return;
        }

        pendingPublishes.Sort((left, right) => left.EventId.CompareTo(right.EventId));
        foreach ((_, string payload) in pendingPublishes)
        {
            await _eventNotifier.PublishAsync(conversationId, payload, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<EventPageDto> SearchEventsAsync(
        string conversationIdEquals,
        string kindEquals,
        DateTimeOffset? timestampGreaterThanOrEqual,
        DateTimeOffset? timestampLessThan,
        EventSortOrder sortOrder,
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit < 1 || limit > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaxPageSize}.");
        }

        IQueryable<ConversationEventRecord> query = BuildFilteredQuery(
            conversationIdEquals,
            kindEquals,
            timestampGreaterThanOrEqual,
            timestampLessThan);

        query = ApplySortOrder(query, sortOrder);

        int offset = DecodePageId(pageId);
        if (offset > 0)
        {
            query = query.Skip(offset);
        }

        List<ConversationEventRecord> records = await query
            .Include(evt => evt.Conversation)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = records.Count > limit;
        if (hasMore)
        {
            records.RemoveAt(limit);
        }

        string nextPageId = hasMore ? EncodePageId(offset + limit) : null;

        List<EventDto> items = records
            .Select(record => MapToDto(record, record.Conversation?.ConversationId ?? string.Empty))
            .ToList();

        return new EventPageDto
        {
            Items = items,
            NextPageId = nextPageId
        };
    }

    public async Task<int> CountEventsAsync(
        string conversationIdEquals,
        string kindEquals,
        DateTimeOffset? timestampGreaterThanOrEqual,
        DateTimeOffset? timestampLessThan,
        EventSortOrder sortOrder,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IQueryable<ConversationEventRecord> query = BuildFilteredQuery(
            conversationIdEquals,
            kindEquals,
            timestampGreaterThanOrEqual,
            timestampLessThan);

        // Sorting is unnecessary for counting but ensures parity with search ordering when translated.
        query = ApplySortOrder(query, sortOrder);

        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventDto>> BatchGetEventsAsync(
        IReadOnlyList<string> eventIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (eventIds.Count == 0)
        {
            return Array.Empty<EventDto>();
        }

        var parsed = eventIds
            .Select((value, index) => ParsedEventId.Parse(index, value))
            .ToList();

        EventDto[] results = new EventDto[eventIds.Count];

        foreach (IGrouping<string, ParsedEventId> grouping in parsed
                     .Where(entry => entry.IsValid)
                     .GroupBy(entry => entry.ConversationId!, StringComparer.OrdinalIgnoreCase))
        {
            string conversationId = grouping.Key;
            ConversationMetadataRecord conversation = await _dbContext.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.ConversationId == conversationId, cancellationToken)
                .ConfigureAwait(false);

            if (conversation is null)
            {
                continue;
            }

            HashSet<int> requestedEventIds = grouping
                .Select(entry => entry.EventId!.Value)
                .ToHashSet();

            List<ConversationEventRecord> events = await _dbContext.ConversationEvents
                .AsNoTracking()
                .Where(evt => evt.ConversationMetadataRecordId == conversation.Id && requestedEventIds.Contains(evt.EventId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Dictionary<int, EventDto> mapped = events
                .ToDictionary(evt => evt.EventId, evt => MapToDto(evt, conversation.ConversationId));

            foreach (ParsedEventId request in grouping)
            {
                if (request.EventId is not int eventId || !mapped.TryGetValue(eventId, out EventDto dto))
                {
                    continue;
                }

                // Assign the same DTO instance to preserve reference equality for identical ids.
                results[request.Index] = dto;
            }
        }

        return results;
    }

    private IQueryable<ConversationEventRecord> BuildFilteredQuery(
        string conversationIdEquals,
        string kindEquals,
        DateTimeOffset? timestampGreaterThanOrEqual,
        DateTimeOffset? timestampLessThan)
    {
        IQueryable<ConversationEventRecord> query = _dbContext.ConversationEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(conversationIdEquals))
        {
            query = query.Where(evt => evt.Conversation.ConversationId == conversationIdEquals);
        }

        if (!string.IsNullOrWhiteSpace(kindEquals))
        {
            query = query.Where(evt => evt.Type == kindEquals);
        }

        if (timestampGreaterThanOrEqual.HasValue)
        {
            query = query.Where(evt => evt.CreatedAtUtc >= timestampGreaterThanOrEqual.Value);
        }

        if (timestampLessThan.HasValue)
        {
            query = query.Where(evt => evt.CreatedAtUtc < timestampLessThan.Value);
        }

        return query;
    }

    private static IQueryable<ConversationEventRecord> ApplySortOrder(
        IQueryable<ConversationEventRecord> query,
        EventSortOrder sortOrder)
    {
        return sortOrder switch
        {
            EventSortOrder.TimestampDesc => query
                .OrderByDescending(evt => evt.CreatedAtUtc)
                .ThenByDescending(evt => evt.Conversation.ConversationId)
                .ThenByDescending(evt => evt.EventId),
            _ => query
                .OrderBy(evt => evt.CreatedAtUtc)
                .ThenBy(evt => evt.Conversation.ConversationId)
                .ThenBy(evt => evt.EventId)
        };
    }

    private static EventDto MapToDto(ConversationEventRecord record, string conversationId)
    {
        using JsonDocument document = JsonDocument.Parse(record.PayloadJson);
        JsonElement payload = document.RootElement.Clone();

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new InvalidOperationException("Conversation identifier is required for events.");
        }

        return new EventDto
        {
            Id = BuildEventId(conversationId, record.EventId),
            ConversationId = conversationId,
            Kind = record.Type,
            Timestamp = record.CreatedAtUtc,
            Payload = payload
        };
    }

    private static string BuildEventId(string conversationId, int eventId)
        => FormattableString.Invariant($"{conversationId}{EventIdSeparator}{eventId}");

    private static int DecodePageId(string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return 0;
        }

        try
        {
            byte[] buffer = Convert.FromBase64String(pageId);
            string decoded = Encoding.UTF8.GetString(buffer);
            if (int.TryParse(decoded, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset) && offset >= 0)
            {
                return offset;
            }
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid page_id cursor.", nameof(pageId), ex);
        }

        throw new ArgumentException("Invalid page_id cursor.", nameof(pageId));
    }

    private static string EncodePageId(int offset)
    {
        string value = offset.ToString(CultureInfo.InvariantCulture);
        byte[] buffer = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(buffer);
    }

    private static string SerializePayload(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : element.GetRawText();
    }

    private readonly record struct ParsedEventId(int Index, string RawValue, string ConversationId, int? EventId)
    {
        public bool IsValid => !string.IsNullOrEmpty(ConversationId) && EventId.HasValue;

        public static ParsedEventId Parse(int index, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new ParsedEventId(index, rawValue, null, null);
            }

            string[] parts = rawValue.Split(EventIdSeparator, 2);
            if (parts.Length != 2)
            {
                return new ParsedEventId(index, rawValue, null, null);
            }

            string conversationId = parts[0];
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return new ParsedEventId(index, rawValue, null, null);
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int eventId) || eventId < 0)
            {
                return new ParsedEventId(index, rawValue, null, null);
            }

            return new ParsedEventId(index, rawValue, conversationId, eventId);
        }
    }
}
