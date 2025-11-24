using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data.Entities.Sandboxes;

namespace NetAI.Api.Data.Repositories;

public class SandboxRepository : ISandboxRepository
{
    private readonly NetAiDbContext _dbContext;

    public SandboxRepository(NetAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SandboxRecord record, CancellationToken cancellationToken)
    {
        _dbContext.Sandboxes.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, SandboxRecord>> BatchGetAsync(
        IReadOnlyCollection<string> sandboxIds,
        CancellationToken cancellationToken)
    {
        if (sandboxIds.Count == 0)
        {
            return new Dictionary<string, SandboxRecord>();
        }

        List<SandboxRecord> records = await _dbContext.Sandboxes
            .AsNoTracking()
            .Where(record => sandboxIds.Contains(record.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.ToDictionary(record => record.Id);
    }

    public async Task DeleteAsync(SandboxRecord record, CancellationToken cancellationToken)
    {
        _dbContext.Sandboxes.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SandboxRecord> GetAsync(string sandboxId, CancellationToken cancellationToken)
    {
        return await _dbContext.Sandboxes
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.Id == sandboxId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<SandboxRecord> Items, string NextPageId)> SearchAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<SandboxRecord> query = _dbContext.Sandboxes.AsNoTracking();

        SandboxRecord cursor = null;
        if (!string.IsNullOrWhiteSpace(pageId))
        {
            cursor = await _dbContext.Sandboxes
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.Id == pageId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (cursor is not null)
        {
            query = query.Where(record =>
                record.CreatedAtUtc < cursor.CreatedAtUtc
                || (record.CreatedAtUtc == cursor.CreatedAtUtc && string.CompareOrdinal(record.Id, cursor.Id) < 0));
        }

        List<SandboxRecord> records = await query
            .OrderByDescending(record => record.CreatedAtUtc)
            .ThenBy(record => record.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string nextPageId = null;
        if (records.Count > limit)
        {
            SandboxRecord next = records[^1];
            nextPageId = next.Id;
            records.RemoveAt(records.Count - 1);
        }

        return (records, nextPageId);
    }

    public async Task UpdateAsync(SandboxRecord record, CancellationToken cancellationToken)
    {
        _dbContext.Sandboxes.Update(record);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
