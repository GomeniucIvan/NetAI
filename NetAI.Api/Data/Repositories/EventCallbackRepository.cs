using System.Linq;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Repositories;

public class EventCallbackRepository : IEventCallbackRepository
{
    private readonly NetAiDbContext _dbContext;

    public EventCallbackRepository(NetAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<EventCallbackRecord> CreateAsync(EventCallbackRecord callback, CancellationToken cancellationToken)
    {
        if (callback.Id == Guid.Empty)
        {
            callback.Id = Guid.NewGuid();
        }

        if (callback.CreatedAtUtc == default)
        {
            callback.CreatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.EventCallbacks.AddAsync(callback, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return callback;
    }

    public async Task<EventCallbackRecord> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.EventCallbacks
            .AsNoTracking()
            .Include(callback => callback.Results)
            .FirstOrDefaultAsync(callback => callback.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventCallbackRecord>> BatchGetAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> idList = ids is null
            ? Array.Empty<Guid>()
            : ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return Array.Empty<EventCallbackRecord>();
        }

        List<EventCallbackRecord> records = await _dbContext.EventCallbacks
            .AsNoTracking()
            .Where(callback => idList.Contains(callback.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return idList
            .Select(id => records.FirstOrDefault(record => record.Id == id))
            .Where(record => record is not null)
            .ToList()!;
    }

    public async Task<(IReadOnlyList<EventCallbackRecord> Items, string NextPageId)> SearchAsync(
        Guid? conversationIdEq,
        string eventKindEq,
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            limit = 1;
        }

        IQueryable<EventCallbackRecord> query = _dbContext.EventCallbacks.AsNoTracking();

        if (conversationIdEq.HasValue)
        {
            Guid conversationId = conversationIdEq.Value;
            query = query.Where(callback => callback.ConversationId == conversationId);
        }

        if (!string.IsNullOrWhiteSpace(eventKindEq))
        {
            string normalizedEventKind = eventKindEq.Trim();
            query = query.Where(callback => callback.EventKind != null
                && callback.EventKind == normalizedEventKind);
        }

        query = query.OrderByDescending(callback => callback.CreatedAtUtc);

        int offset = 0;
        if (!string.IsNullOrWhiteSpace(pageId) && int.TryParse(pageId, out int parsedOffset) && parsedOffset >= 0)
        {
            offset = parsedOffset;
        }

        query = query.Skip(offset).Take(limit + 1);

        List<EventCallbackRecord> results = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
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
        EventCallbackRecord existing = await _dbContext.EventCallbacks
            .FirstOrDefaultAsync(callback => callback.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        _dbContext.EventCallbacks.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
