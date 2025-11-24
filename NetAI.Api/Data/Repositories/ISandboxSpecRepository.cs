using NetAI.Api.Data.Entities.Sandboxes;

namespace NetAI.Api.Data.Repositories;

public interface ISandboxSpecRepository
{
    Task<SandboxSpecRecord> GetAsync(string sandboxSpecId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, SandboxSpecRecord>> BatchGetAsync(
        IReadOnlyCollection<string> sandboxSpecIds,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<SandboxSpecRecord> Items, string NextPageId)> SearchAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken);
}
