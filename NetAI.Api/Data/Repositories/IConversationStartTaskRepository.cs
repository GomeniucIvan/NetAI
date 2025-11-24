using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Data.Repositories;

public interface IConversationStartTaskRepository
{
    Task<ConversationStartTaskRecord> AddAsync(ConversationStartTaskRecord record, CancellationToken cancellationToken);

    Task<ConversationStartTaskRecord> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationStartTaskRecord>> BatchGetAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task UpdateAsync(ConversationStartTaskRecord record, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ConversationStartTaskRecord> Items, string NextPageId)> SearchAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task<int> CleanupCompletedAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);

    Task<int> CountAsync(string conversationId, CancellationToken cancellationToken);
}
