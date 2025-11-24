using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;

public class AppConversationInfoService : IAppConversationInfoService
{
    private readonly IAppConversationInfoRepository _repository;

    public AppConversationInfoService(IAppConversationInfoRepository repository)
    {
        _repository = repository;
    }

    public async Task<AppConversationPageDto> SearchAsync(AppConversationSearchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request ??= new AppConversationSearchRequest();

        AppConversationSortOrder sortOrder = ParseSortOrder(request.SortOrder);
        int limit = Math.Clamp(request.Limit.GetValueOrDefault(100), 1, 100);
        int offset = ParseOffset(request.PageId);

        (IReadOnlyList<ConversationMetadataRecord> Items, string NextPageId) result = await _repository
            .SearchAsync(
                sortOrder,
                request.TitleContains,
                request.CreatedAtGte,
                request.CreatedAtLt,
                request.UpdatedAtGte,
                request.UpdatedAtLt,
                offset,
                limit,
                request.UserId,
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AppConversationDto> items = result.Items
            .Select(ToDto)
            .ToList();

        return new AppConversationPageDto
        {
            Items = items,
            NextPageId = result.NextPageId,
        };
    }

    public async Task<int> CountAsync(AppConversationCountRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request ??= new AppConversationCountRequest();

        int count = await _repository
            .CountAsync(
                request.TitleContains,
                request.CreatedAtGte,
                request.CreatedAtLt,
                request.UpdatedAtGte,
                request.UpdatedAtLt,
                request.UserId,
                cancellationToken)
            .ConfigureAwait(false);

        return count;
    }

    public async Task<IReadOnlyList<AppConversationDto>> GetByIdsAsync(
        IReadOnlyList<Guid> conversationIds,
        string userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (conversationIds is null)
        {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        if (conversationIds.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(conversationIds), "A maximum of 100 conversations may be requested at once.");
        }

        IReadOnlyList<ConversationMetadataRecord> records = await _repository
            .GetByConversationIdsAsync(conversationIds, userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, AppConversationDto> lookup = records
            .Select(record => (record, conversationId: ParseConversationId(record.ConversationId)))
            .Where(tuple => tuple.conversationId.HasValue)
            .Select(tuple => new KeyValuePair<Guid, AppConversationDto>(tuple.conversationId!.Value, ToDto(tuple.record)))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        List<AppConversationDto> results = new(conversationIds.Count);
        foreach (Guid conversationId in conversationIds)
        {
            results.Add(lookup.TryGetValue(conversationId, out AppConversationDto dto) ? dto : null);
        }

        return results;
    }

    private static AppConversationDto ToDto(ConversationMetadataRecord record)
    {
        Guid id = ParseConversationId(record.ConversationId) ?? Guid.Empty;
        DateTime created = EnsureUtc(record.CreatedAtUtc);
        DateTimeOffset createdAt = new(created, TimeSpan.Zero);
        DateTimeOffset? updatedAt = record.LastUpdatedAtUtc.HasValue
            ? new DateTimeOffset(EnsureUtc(record.LastUpdatedAtUtc.Value), TimeSpan.Zero)
            : null;

        AppConversationMetricsDto metrics = BuildMetrics(record);

        return new AppConversationDto
        {
            Id = id,
            CreatedByUserId = record.UserId,
            SandboxId = record.SandboxId,
            SelectedRepository = record.SelectedRepository,
            SelectedBranch = record.SelectedBranch,
            GitProvider = record.GitProvider?.ToString(),
            Title = record.Title,
            Trigger = record.Trigger?.ToString(),
            PullRequestNumbers = record.PullRequestNumbers,
            LlmModel = record.LlmModel,
            Metrics = metrics,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            SandboxStatus = record.Status.ToString().ToUpperInvariant(),
            AgentStatus = record.RuntimeStatus,
            ConversationUrl = record.Url,
            SessionApiKey = record.SessionApiKey,
            SandboxVscodeUrl = record.VscodeUrl,
        };
    }

    private static AppConversationMetricsDto BuildMetrics(ConversationMetadataRecord record)
    {
        return new AppConversationMetricsDto
        {
            AccumulatedCost = record.AccumulatedCost,
            MaxBudgetPerTask = null,
            AccumulatedTokenUsage = new AppConversationTokenUsageDto
            {
                PromptTokens = record.PromptTokens,
                CompletionTokens = record.CompletionTokens,
                TotalTokens = record.TotalTokens,
                CacheReadTokens = null,
                CacheWriteTokens = null,
                ReasoningTokens = null,
                ContextWindow = null,
                PerTurnToken = null,
            },
        };
    }

    private static int ParseOffset(string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return 0;
        }

        if (int.TryParse(pageId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset) && offset >= 0)
        {
            return offset;
        }

        return 0;
    }

    private static AppConversationSortOrder ParseSortOrder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AppConversationSortOrder.CreatedAtDesc;
        }

        if (Enum.TryParse<AppConversationSortOrder>(value, ignoreCase: true, out var sortOrder))
        {
            return sortOrder;
        }

        return AppConversationSortOrder.CreatedAtDesc;
    }

    private static Guid? ParseConversationId(string value)
    {
        if (Guid.TryParse(value, out Guid id))
        {
            return id;
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
