using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Repositories;

public interface IEventCallbackRepository
{
    Task<EventCallbackRecord> CreateAsync(EventCallbackRecord callback, CancellationToken cancellationToken);

    Task<EventCallbackRecord> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EventCallbackRecord>> BatchGetAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    Task<(IReadOnlyList<EventCallbackRecord> Items, string NextPageId)> SearchAsync(
        Guid? conversationIdEq,
        string eventKindEq,
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
