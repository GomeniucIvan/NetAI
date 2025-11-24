using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Repositories;

public class ConversationStartTaskRepository : IConversationStartTaskRepository
{
    private readonly NetAiDbContext _dbContext;
    private readonly ILogger<ConversationStartTaskRepository> _logger;

    public ConversationStartTaskRepository(NetAiDbContext dbContext, ILogger<ConversationStartTaskRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ConversationStartTaskRecord> AddAsync(ConversationStartTaskRecord record, CancellationToken cancellationToken)
    {
        record.CreatedAtUtc = DateTimeOffset.UtcNow;
        record.UpdatedAtUtc = record.CreatedAtUtc;
        await _dbContext.ConversationStartTasks.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted conversation start task {TaskId} for user {UserId} with initial status {Status}",
            record.Id,
            record.CreatedByUserId,
            record.Status);

        return record;
    }

    public async Task<ConversationStartTaskRecord> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ConversationStartTaskRecord record = await _dbContext.ConversationStartTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(task => task.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            _logger.LogWarning("Requested conversation start task {TaskId} was not found", id);
        }
        else
        {
            _logger.LogInformation(
                "Loaded conversation start task {TaskId} with status {Status} and conversation {ConversationId}",
                record.Id,
                record.Status,
                record.AppConversationId);
        }

        return record;
    }

    public async Task<IReadOnlyList<ConversationStartTaskRecord>> BatchGetAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ConversationStartTaskRecord>();
        }

        IReadOnlyDictionary<Guid, ConversationStartTaskRecord> lookup = await _dbContext.ConversationStartTasks
            .AsNoTracking()
            .Where(task => ids.Contains(task.Id))
            .ToDictionaryAsync(task => task.Id, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ConversationStartTaskRecord> results = ids
            .Select(id => lookup.TryGetValue(id, out ConversationStartTaskRecord record) ? record : null)
            .ToList();

        int foundCount = results.Count(record => record is not null);
        if (foundCount > 0)
        {
            _logger.LogInformation("Batch loaded {Found}/{Requested} conversation start tasks", foundCount, ids.Count);
        }
        else if (ids.Count > 0)
        {
            _logger.LogWarning("Batch load returned no conversation start tasks for {Requested} ids", ids.Count);
        }

        return results;
    }

    public async Task UpdateAsync(ConversationStartTaskRecord record, CancellationToken cancellationToken)
    {
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _dbContext.ConversationStartTasks.Update(record);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated conversation start task {TaskId} to status {Status}. Detail={Detail}; Failure={Failure}",
            record.Id,
            record.Status,
            record.Detail,
            record.FailureDetail);
    }

    public async Task<(IReadOnlyList<ConversationStartTaskRecord> Items, string NextPageId)> SearchAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<ConversationStartTaskRecord> query = _dbContext.ConversationStartTasks.AsNoTracking();

        if (TryParsePageId(pageId, out DateTimeOffset cursor))
        {
            query = query.Where(task => task.CreatedAtUtc < cursor);
        }

        query = query.OrderByDescending(task => task.CreatedAtUtc);

        List<ConversationStartTaskRecord> results = await query
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = results.Count > limit;
        if (hasMore)
        {
            results = results.Take(limit).ToList();
        }

        string nextPageId = null;
        if (hasMore && results.Count > 0)
        {
            ConversationStartTaskRecord last = results[^1];
            nextPageId = CreatePageId(last);
        }

        if (results.Count > 0)
        {
            _logger.LogInformation(
                "Retrieved {Count} conversation start tasks during search. NextPageId={NextPageId}",
                results.Count,
                nextPageId ?? "<null>");
        }
        else
        {
            _logger.LogInformation("Conversation start task search returned no results");
        }

        return (results, nextPageId);
    }

    public Task<int> CountAsync(string conversationId, CancellationToken cancellationToken)
    {
        IQueryable<ConversationStartTaskRecord> query = _dbContext.ConversationStartTasks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            query = query.Where(task => task.AppConversationId == conversationId);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task<int> CleanupCompletedAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        List<ConversationStartTaskRecord> expired = await _dbContext.ConversationStartTasks
            .Where(task =>
                (task.Status == ConversationStartTaskStatus.Ready || task.Status == ConversationStartTaskStatus.Error)
                && task.CompletedAtUtc.HasValue
                && task.CompletedAtUtc <= olderThan)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            _logger.LogDebug("No completed conversation start tasks eligible for cleanup before {Threshold}", olderThan);
            return 0;
        }

        _dbContext.ConversationStartTasks.RemoveRange(expired);
        int removed = await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Removed {Removed} completed conversation start tasks older than {Threshold}",
            removed,
            olderThan);
        return removed;
    }

    private static bool TryParsePageId(string pageId, out DateTimeOffset cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return false;
        }

        string[] parts = pageId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[0], out long milliseconds))
        {
            return false;
        }

        try
        {
            cursor = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string CreatePageId(ConversationStartTaskRecord record)
    {
        long milliseconds = record.CreatedAtUtc.ToUnixTimeMilliseconds();
        return $"{milliseconds}:{record.Id}";
    }
}
