using NetAI.Api.Data.Entities.Conversations;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Data.Repositories;

public interface IConversationRepository
{
    Task<ConversationInfoResultSetDto> GetConversationsAsync(
        int limit,
        string pageId,
        string selectedRepository,
        string conversationTrigger,
        CancellationToken cancellationToken);

    Task<ConversationMetadataRecord> GetConversationAsync(
        string conversationId,
        bool includeDetails,
        CancellationToken cancellationToken);

    Task<ConversationMetadataRecord> CreateConversationAsync(
        ConversationMetadataRecord record,
        CancellationToken cancellationToken);

    Task<bool> DeleteConversationAsync(string conversationId, CancellationToken cancellationToken);

    Task<int> GetNextEventIdAsync(Guid conversationRecordId, CancellationToken cancellationToken);

    Task<ConversationEventRecord> AddEventAsync(
        ConversationEventRecord record,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationEventRecord>> GetEventsAsync(
        Guid conversationRecordId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationEventRecord>> GetEventsAsync(
        Guid conversationRecordId,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        CancellationToken cancellationToken);

    Task<ConversationRememberPromptRecord> GetRememberPromptAsync(
        Guid conversationRecordId,
        int eventId,
        CancellationToken cancellationToken);

    Task SetRememberPromptAsync(
        Guid conversationRecordId,
        int eventId,
        string prompt,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
