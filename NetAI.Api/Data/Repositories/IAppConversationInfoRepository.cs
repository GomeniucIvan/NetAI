using System;
using System.Collections.Generic;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Data.Repositories;

public interface IAppConversationInfoRepository
{
    Task<(IReadOnlyList<ConversationMetadataRecord> Items, string NextPageId)> SearchAsync(
        AppConversationSortOrder sortOrder,
        string titleContains,
        DateTimeOffset? createdAtGte,
        DateTimeOffset? createdAtLt,
        DateTimeOffset? updatedAtGte,
        DateTimeOffset? updatedAtLt,
        int offset,
        int limit,
        string userId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        string titleContains,
        DateTimeOffset? createdAtGte,
        DateTimeOffset? createdAtLt,
        DateTimeOffset? updatedAtGte,
        DateTimeOffset? updatedAtLt,
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationMetadataRecord>> GetByConversationIdsAsync(
        IReadOnlyList<Guid> conversationIds,
        string userId,
        CancellationToken cancellationToken);
}
