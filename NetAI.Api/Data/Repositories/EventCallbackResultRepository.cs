using System.Linq;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Repositories;

public class EventCallbackResultRepository : IEventCallbackResultRepository
{
    private readonly NetAiDbContext _dbContext;

    public EventCallbackResultRepository(NetAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<EventCallbackResultRecord> CreateAsync(EventCallbackResultRecord result, CancellationToken cancellationToken)
    {
        if (result.Id == Guid.Empty)
        {
            result.Id = Guid.NewGuid();
        }

        if (result.CreatedAtUtc == default)
        {
            result.CreatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.EventCallbackResults.AddAsync(result, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<EventCallbackResultRecord> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.EventCallbackResults
            .AsNoTracking()
            .FirstOrDefaultAsync(result => result.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventCallbackResultRecord>> BatchGetAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> idList = ids is null
            ? Array.Empty<Guid>()
            : ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return Array.Empty<EventCallbackResultRecord>();
        }

        List<EventCallbackResultRecord> records = await _dbContext.EventCallbackResults
            .AsNoTracking()
            .Where(result => idList.Contains(result.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return idList
            .Select(id => records.FirstOrDefault(record => record.Id == id))
            .Where(record => record is not null)
            .ToList()!;
    }

    public async Task<(IReadOnlyList<EventCallbackResultRecord> Items, string NextPageId)> SearchAsync(
        Guid? conversationIdEq,
        Guid? eventCallbackIdEq,
        Guid? eventIdEq,
        EventCallbackResultStatus? statusEq,
        EventCallbackResultSortOrder sortOrder,
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            limit = 1;
        }

        IQueryable<EventCallbackResultRecord> query = _dbContext.EventCallbackResults.AsNoTracking();

        if (conversationIdEq.HasValue)
        {
            Guid conversationId = conversationIdEq.Value;
            query = query.Where(result => result.ConversationId == conversationId);
        }

        if (eventCallbackIdEq.HasValue)
        {
            Guid eventCallbackId = eventCallbackIdEq.Value;
            query = query.Where(result => result.EventCallbackId == eventCallbackId);
        }

        if (eventIdEq.HasValue)
        {
            Guid eventId = eventIdEq.Value;
            query = query.Where(result => result.EventId == eventId);
        }

        if (statusEq.HasValue)
        {
            EventCallbackResultStatus status = statusEq.Value;
            query = query.Where(result => result.Status == status);
        }

        query = sortOrder switch
        {
            EventCallbackResultSortOrder.CreatedAt => query.OrderBy(result => result.CreatedAtUtc),
            _ => query.OrderByDescending(result => result.CreatedAtUtc)
        };

        int offset = 0;
        if (!string.IsNullOrWhiteSpace(pageId) && int.TryParse(pageId, out int parsedOffset) && parsedOffset >= 0)
        {
            offset = parsedOffset;
        }

        query = query.Skip(offset).Take(limit + 1);

        List<EventCallbackResultRecord> results = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        bool hasMore = results.Count > limit;
        if (hasMore)
        {
            results = results.Take(limit).ToList();
        }

        string nextPageId = hasMore ? (offset + limit).ToString() : null;

        return (results, nextPageId);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        EventCallbackResultRecord existing = await _dbContext.EventCallbackResults
            .FirstOrDefaultAsync(result => result.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        _dbContext.EventCallbackResults.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
