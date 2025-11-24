using NetAI.Api.Data.Entities.Sandboxes;

namespace NetAI.Api.Data.Repositories;

public interface ISandboxRepository
{
    Task<SandboxRecord> GetAsync(string sandboxId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SandboxRecord>> BatchGetAsync(
        IReadOnlyCollection<string> sandboxIds,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<SandboxRecord> Items, string NextPageId)> SearchAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task AddAsync(SandboxRecord record, CancellationToken cancellationToken);

    Task UpdateAsync(SandboxRecord record, CancellationToken cancellationToken);

    Task DeleteAsync(SandboxRecord record, CancellationToken cancellationToken);
}
