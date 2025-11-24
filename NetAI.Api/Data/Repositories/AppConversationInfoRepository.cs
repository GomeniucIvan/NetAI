using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Data.Repositories;

public class AppConversationInfoRepository : IAppConversationInfoRepository
{
    private const string ConversationVersionV1 = "V1"; //todo remove
    private readonly NetAiDbContext _dbContext;

    public AppConversationInfoRepository(NetAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<ConversationMetadataRecord> Items, string NextPageId)> SearchAsync(
        AppConversationSortOrder sortOrder,
        string titleContains,
        DateTimeOffset? createdAtGte,
        DateTimeOffset? createdAtLt,
        DateTimeOffset? updatedAtGte,
        DateTimeOffset? updatedAtLt,
        int offset,
        int limit,
        string userId,
        CancellationToken cancellationToken)
    {
        IQueryable<ConversationMetadataRecord> query = BaseQuery(userId);
        query = ApplyFilters(query, titleContains, createdAtGte, createdAtLt, updatedAtGte, updatedAtLt);
        query = ApplySortOrder(query, sortOrder);

        if (offset > 0)
        {
            query = query.Skip(offset);
        }

        List<ConversationMetadataRecord> results = await query
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = results.Count > limit;
        if (hasMore)
        {
            results = results.Take(limit).ToList();
        }

        string nextPageId = hasMore
            ? (offset + limit).ToString(CultureInfo.InvariantCulture)
            : null;

        return (results, nextPageId);
    }

    public async Task<int> CountAsync(
        string titleContains,
        DateTimeOffset? createdAtGte,
        DateTimeOffset? createdAtLt,
        DateTimeOffset? updatedAtGte,
        DateTimeOffset? updatedAtLt,
        string userId,
        CancellationToken cancellationToken)
    {
        IQueryable<ConversationMetadataRecord> query = BaseQuery(userId);
        query = ApplyFilters(query, titleContains, createdAtGte, createdAtLt, updatedAtGte, updatedAtLt);
        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConversationMetadataRecord>> GetByConversationIdsAsync(
        IReadOnlyList<Guid> conversationIds,
        string userId,
        CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
        {
            return Array.Empty<ConversationMetadataRecord>();
        }

        List<string> identifiers = conversationIds.Select(id => id.ToString()).ToList();

        IQueryable<ConversationMetadataRecord> query = BaseQuery(userId)
            .Where(record => identifiers.Contains(record.ConversationId));

        List<ConversationMetadataRecord> records = await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records;
    }

    private IQueryable<ConversationMetadataRecord> BaseQuery(string userId)
    {
        IQueryable<ConversationMetadataRecord> query = _dbContext.Conversations
            .AsNoTracking()
            .Where(record => record.ConversationVersion == ConversationVersionV1);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(record => record.UserId == userId);
        }

        return query;
    }

    private static IQueryable<ConversationMetadataRecord> ApplyFilters(
        IQueryable<ConversationMetadataRecord> query,
        string titleContains,
        DateTimeOffset? createdAtGte,
        DateTimeOffset? createdAtLt,
        DateTimeOffset? updatedAtGte,
        DateTimeOffset? updatedAtLt)
    {
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            string search = $"%{titleContains.Trim()}%";
            query = query.Where(record => record.Title != null && EF.Functions.ILike(record.Title, search));
        }

        if (createdAtGte.HasValue)
        {
            DateTime createdAtLower = createdAtGte.Value.UtcDateTime;
            query = query.Where(record => record.CreatedAtUtc >= createdAtLower);
        }

        if (createdAtLt.HasValue)
        {
            DateTime createdAtUpper = createdAtLt.Value.UtcDateTime;
            query = query.Where(record => record.CreatedAtUtc < createdAtUpper);
        }

        if (updatedAtGte.HasValue)
        {
            DateTime updatedLower = updatedAtGte.Value.UtcDateTime;
            query = query.Where(record => record.LastUpdatedAtUtc >= updatedLower);
        }

        if (updatedAtLt.HasValue)
        {
            DateTime updatedUpper = updatedAtLt.Value.UtcDateTime;
            query = query.Where(record => record.LastUpdatedAtUtc < updatedUpper);
        }

        return query;
    }

    private static IQueryable<ConversationMetadataRecord> ApplySortOrder(
        IQueryable<ConversationMetadataRecord> query,
        AppConversationSortOrder sortOrder)
    {
        return sortOrder switch
        {
            AppConversationSortOrder.CreatedAt => query
                .OrderBy(record => record.CreatedAtUtc)
                .ThenBy(record => record.ConversationId),
            AppConversationSortOrder.CreatedAtDesc => query
                .OrderByDescending(record => record.CreatedAtUtc)
                .ThenByDescending(record => record.ConversationId),
            AppConversationSortOrder.UpdatedAt => query
                .OrderBy(record => record.LastUpdatedAtUtc ?? record.CreatedAtUtc)
                .ThenBy(record => record.ConversationId),
            AppConversationSortOrder.UpdatedAtDesc => query
                .OrderByDescending(record => record.LastUpdatedAtUtc ?? record.CreatedAtUtc)
                .ThenByDescending(record => record.ConversationId),
            AppConversationSortOrder.Title => query
                .OrderBy(record => record.Title)
                .ThenBy(record => record.ConversationId),
            AppConversationSortOrder.TitleDesc => query
                .OrderByDescending(record => record.Title)
                .ThenByDescending(record => record.ConversationId),
            _ => query
                .OrderByDescending(record => record.CreatedAtUtc)
                .ThenByDescending(record => record.ConversationId),
        };
    }
}
