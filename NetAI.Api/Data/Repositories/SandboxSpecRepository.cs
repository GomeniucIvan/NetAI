using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data.Entities.Sandboxes;

namespace NetAI.Api.Data.Repositories;

public class SandboxSpecRepository : ISandboxSpecRepository
{
    private readonly NetAiDbContext _dbContext;

    public SandboxSpecRepository(NetAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<string, SandboxSpecRecord>> BatchGetAsync(
        IReadOnlyCollection<string> sandboxSpecIds,
        CancellationToken cancellationToken)
    {
        if (sandboxSpecIds.Count == 0)
        {
            return new Dictionary<string, SandboxSpecRecord>();
        }

        List<SandboxSpecRecord> records = await _dbContext.SandboxSpecs
            .AsNoTracking()
            .Where(record => sandboxSpecIds.Contains(record.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.ToDictionary(record => record.Id);
    }

    public async Task<SandboxSpecRecord> GetAsync(string sandboxSpecId, CancellationToken cancellationToken)
    {
        return await _dbContext.SandboxSpecs
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.Id == sandboxSpecId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<SandboxSpecRecord> Items, string NextPageId)> SearchAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<SandboxSpecRecord> query = _dbContext.SandboxSpecs.AsNoTracking();

        SandboxSpecRecord cursor = null;
        if (!string.IsNullOrWhiteSpace(pageId))
        {
            cursor = await _dbContext.SandboxSpecs
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

        List<SandboxSpecRecord> records = await query
            .OrderByDescending(record => record.CreatedAtUtc)
            .ThenBy(record => record.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string nextPageId = null;
        if (records.Count > limit)
        {
            SandboxSpecRecord next = records[^1];
            nextPageId = next.Id;
            records.RemoveAt(records.Count - 1);
        }

        return (records, nextPageId);
    }
}
