using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Repositories;

public interface IEventCallbackResultRepository
{
    Task<EventCallbackResultRecord> CreateAsync(EventCallbackResultRecord result, CancellationToken cancellationToken);

    Task<EventCallbackResultRecord> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EventCallbackResultRecord>> BatchGetAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    Task<(IReadOnlyList<EventCallbackResultRecord> Items, string NextPageId)> SearchAsync(
        Guid? conversationIdEq,
        Guid? eventCallbackIdEq,
        Guid? eventIdEq,
        EventCallbackResultStatus? statusEq,
        EventCallbackResultSortOrder sortOrder,
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
