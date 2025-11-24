using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetAI.Api.Data.Entities.Conversations;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;
using NetAI.Api.Services.Http;

namespace NetAI.Api.Data.Repositories;

public class ConversationRepository : IConversationRepository
{
    public const string HttpClientName = "OpenHands.Conversations";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RemoteFailureCooldown = TimeSpan.FromSeconds(30);
    private static long _remoteRetryAfterUtcMs;

    private readonly NetAiDbContext _dbContext;
    private readonly IHttpClientSelector _httpClientSelector;
    private readonly ConversationRepositoryOptions _options;
    private readonly ILogger<ConversationRepository> _logger;

    public ConversationRepository(NetAiDbContext dbContext)
        : this(
            dbContext,
            new HttpClientSelector(
                NullHttpClientFactory.Instance,
                Options.Create(new RuntimeConversationGatewayOptions()),
                NullLogger<HttpClientSelector>.Instance),
            Options.Create(new ConversationRepositoryOptions()),
            NullLogger<ConversationRepository>.Instance)
    {
    }

    public ConversationRepository(
        NetAiDbContext dbContext,
        IHttpClientSelector httpClientSelector,
        IOptions<ConversationRepositoryOptions> options,
        ILogger<ConversationRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _httpClientSelector = httpClientSelector ?? throw new ArgumentNullException(nameof(httpClientSelector));
        _options = options?.Value ?? new ConversationRepositoryOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConversationInfoResultSetDto> GetConversationsAsync(
        int limit,
        string pageId,
        string selectedRepository,
        string conversationTrigger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HttpClient client = CreateHttpClient();
        if (!HasRemoteEndpoint(client))
        {
            _logger.LogDebug("No OpenHands conversation endpoint configured. Falling back to local database search.");
            return await FetchFromDatabaseAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken)
                .ConfigureAwait(false);
        }

        if (ShouldBypassRemote())
        {
            _logger.LogDebug(
                "Skipping OpenHands conversation API call due to recent failures within the cooldown window.");
            return await FetchFromDatabaseAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken)
                .ConfigureAwait(false);
        }

        string requestUri = BuildRequestUri(limit, pageId, selectedRepository, conversationTrigger);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
            using HttpResponseMessage response = await client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                RegisterRemoteFailure();
                _logger.LogWarning(
                    "OpenHands conversation API returned status {StatusCode}. Falling back to local database search.",
                    response.StatusCode);
                return await FetchFromDatabaseAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken)
                    .ConfigureAwait(false);
            }

            await using Stream content = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            ConversationInfoResultSetDto remoteResult = await JsonSerializer.DeserializeAsync<ConversationInfoResultSetDto>(
                content,
                SerializerOptions,
                cancellationToken)
                .ConfigureAwait(false);

            if (remoteResult is null)
            {
                RegisterRemoteFailure();
                _logger.LogWarning("Received empty conversation payload from OpenHands API. Falling back to local database search.");
                return await FetchFromDatabaseAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken)
                    .ConfigureAwait(false);
            }

            await SynchronizeDatabaseAsync(remoteResult.Results, cancellationToken).ConfigureAwait(false);

            ResetRemoteFailureState();

            return remoteResult;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(
                ex,
                "Failed to retrieve conversations from OpenHands API. Falling back to local database search.");
            RegisterRemoteFailure();
            return await FetchFromDatabaseAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private HttpClient CreateHttpClient()
    {
        string baseUrl = _options.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = Environment.GetEnvironmentVariable("RUNTIME_GATEWAY_BASE_URL")
                ?? Environment.GetEnvironmentVariable("NETAI_RUNTIME_BASE_URL");
        }

        return _httpClientSelector.GetExternalClient(HttpClientName, baseUrl);
    }

    private static bool HasRemoteEndpoint(HttpClient client)
    {
        return client.BaseAddress is not null;
    }

    private static bool ShouldBypassRemote()
    {
        long disabledUntil = Volatile.Read(ref _remoteRetryAfterUtcMs);
        if (disabledUntil <= 0)
        {
            return false;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now < disabledUntil)
        {
            return true;
        }

        Interlocked.CompareExchange(ref _remoteRetryAfterUtcMs, 0, disabledUntil);
        return false;
    }

    private static string BuildRequestUri(
        int limit,
        string pageId,
        string selectedRepository,
        string conversationTrigger)
    {
        int sanitizedLimit = limit <= 0 ? 1 : limit;

        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["limit"] = sanitizedLimit.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(pageId))
        {
            query["page_id"] = pageId;
        }

        if (!string.IsNullOrWhiteSpace(selectedRepository))
        {
            query["selected_repository"] = selectedRepository.Trim();
        }

        if (!string.IsNullOrWhiteSpace(conversationTrigger))
        {
            query["conversation_trigger"] = conversationTrigger.Trim();
        }

        return QueryHelpers.AddQueryString("/api/conversations", query);
    }

    private async Task<ConversationInfoResultSetDto> FetchFromDatabaseAsync(
        int limit,
        string pageId,
        string selectedRepository,
        string conversationTrigger,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            limit = 1;
        }

        IQueryable<ConversationMetadataRecord> query = _dbContext.Conversations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(selectedRepository))
        {
            string repositoryFilter = selectedRepository.Trim();
            query = query.Where(record => record.SelectedRepository != null
                && string.Equals(record.SelectedRepository, repositoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(conversationTrigger)
            && Enum.TryParse<ConversationTrigger>(conversationTrigger, true, out var trigger))
        {
            query = query.Where(record => record.Trigger == trigger);
        }

        query = query
            .OrderByDescending(record => record.LastUpdatedAtUtc ?? record.CreatedAtUtc)
            .ThenByDescending(record => record.CreatedAtUtc)
            .ThenByDescending(record => record.ConversationId);

        DateTime? anchorSortTimestamp = null;
        DateTime anchorCreatedAt = default;

        if (!string.IsNullOrWhiteSpace(pageId))
        {
            var anchor = await _dbContext.Conversations
                .AsNoTracking()
                .Where(record => record.ConversationId == pageId)
                .Select(record => new
                {
                    SortTimestamp = record.LastUpdatedAtUtc ?? record.CreatedAtUtc,
                    record.CreatedAtUtc
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (anchor is not null)
            {
                anchorSortTimestamp = anchor.SortTimestamp;
                anchorCreatedAt = anchor.CreatedAtUtc;
            }
        }

        if (anchorSortTimestamp.HasValue)
        {
            DateTime sortTimestamp = anchorSortTimestamp.Value;
            DateTime createdAt = anchorCreatedAt;
            string anchorConversationId = pageId!;

            query = query.Where(record =>
                (record.LastUpdatedAtUtc ?? record.CreatedAtUtc) < sortTimestamp
                || ((record.LastUpdatedAtUtc ?? record.CreatedAtUtc) == sortTimestamp
                    && (record.CreatedAtUtc < createdAt
                        || (record.CreatedAtUtc == createdAt
                            && string.Compare(record.ConversationId ?? string.Empty, anchorConversationId) < 0))));
        }

        List<ConversationMetadataRecord> page;
        try
        {
            page = await query
                .Take(limit + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Conversation query cancelled before completion. Returning empty result set.");
            return EmptyResult();
        }

        bool hasMore = page.Count > limit;
        string nextPageId = null;

        if (hasMore)
        {
            nextPageId = page[limit].ConversationId;
            page = page.Take(limit).ToList();
        }

        var dto = new ConversationInfoResultSetDto
        {
            Results = page.Select(ConvertToConversationInfo).ToList(),
            NextPageId = nextPageId
        };

        return dto;
    }

    private static ConversationInfoResultSetDto EmptyResult()
    {
        return new ConversationInfoResultSetDto
        {
            Results = Array.Empty<ConversationInfoDto>(),
            NextPageId = null
        };
    }

    private async Task SynchronizeDatabaseAsync(
        IReadOnlyList<ConversationInfoDto> conversations,
        CancellationToken cancellationToken)
    {
        if (conversations.Count == 0)
        {
            return;
        }

        List<string> conversationIds = conversations
            .Where(info => !string.IsNullOrWhiteSpace(info.ConversationId))
            .Select(info => info.ConversationId)
            .ToList();

        if (conversationIds.Count == 0)
        {
            return;
        }

        List<ConversationMetadataRecord> existingRecords = await _dbContext.Conversations
            .Where(record => conversationIds.Contains(record.ConversationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingById = existingRecords.ToDictionary(record => record.ConversationId, StringComparer.Ordinal);

        foreach (ConversationInfoDto conversation in conversations)
        {
            if (string.IsNullOrWhiteSpace(conversation.ConversationId))
            {
                continue;
            }

            if (!existingById.TryGetValue(conversation.ConversationId, out ConversationMetadataRecord record))
            {
                record = new ConversationMetadataRecord
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversation.ConversationId
                };
                _dbContext.Conversations.Add(record);
                existingById[conversation.ConversationId] = record;
            }

            ApplyConversationInfo(record, conversation);
        }

        _dbContext.ChangeTracker.DetectChanges();
        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ApplyConversationInfo(
        ConversationMetadataRecord record,
        ConversationInfoDto conversation)
    {
        record.Title = string.IsNullOrWhiteSpace(conversation.Title)
            ? conversation.ConversationId
            : conversation.Title;
        record.SelectedRepository = conversation.SelectedRepository;
        record.SelectedBranch = conversation.SelectedBranch;

        record.GitProviderRaw = string.IsNullOrWhiteSpace(conversation.GitProvider)
            ? null
            : conversation.GitProvider;
        record.GitProvider = TryParseProvider(conversation.GitProvider);

        record.Trigger = TryParseTrigger(conversation.Trigger);

        ConversationStatus fallbackStatus = record.Status;
        record.Status = TryParseConversationStatus(conversation.Status, fallbackStatus);
        record.RuntimeStatus = conversation.RuntimeStatus;

        record.Url = conversation.Url;
        record.SessionApiKey = conversation.SessionApiKey;

        record.CreatedAtUtc = conversation.CreatedAt.UtcDateTime;
        record.LastUpdatedAtUtc = (conversation.LastUpdatedAt ?? conversation.CreatedAt).UtcDateTime;

        record.PullRequestNumbers = conversation.PullRequestNumbers?.ToList() ?? new List<int>();

        string version = conversation.ConversationVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            version = record.ConversationVersion;
        }

        record.ConversationVersion = string.IsNullOrWhiteSpace(version) ? "V0" : version;
    }

    private static ConversationInfoDto ConvertToConversationInfo(ConversationMetadataRecord record)
    {
        return new ConversationInfoDto
        {
            ConversationId = record.ConversationId,
            Title = string.IsNullOrWhiteSpace(record.Title) ? record.ConversationId : record.Title!,
            LastUpdatedAt = record.LastUpdatedAtUtc.HasValue
                ? new DateTimeOffset(record.LastUpdatedAtUtc.Value, TimeSpan.Zero)
                : null,
            Status = record.Status.ToString().ToUpperInvariant(),
            RuntimeStatus = record.RuntimeStatus,
            SelectedRepository = record.SelectedRepository,
            SelectedBranch = record.SelectedBranch,
            GitProvider = record.GitProviderRaw ?? record.GitProvider?.ToString(),
            Trigger = record.Trigger?.ToString(),
            NumConnections = 0,
            Url = record.Url,
            SessionApiKey = record.SessionApiKey,
            CreatedAt = new DateTimeOffset(record.CreatedAtUtc, TimeSpan.Zero),
            PullRequestNumbers = record.PullRequestNumbers.ToList(),
            ConversationVersion = record.ConversationVersion
        };
    }

    private static ConversationStatus TryParseConversationStatus(string status, ConversationStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return fallback;
        }

        return Enum.TryParse<ConversationStatus>(status, true, out var parsed) ? parsed : fallback;
    }

    private static ConversationTrigger? TryParseTrigger(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return null;
        }

        return Enum.TryParse<ConversationTrigger>(trigger, true, out var parsed) ? parsed : null;
    }

    private static ProviderType? TryParseProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return Enum.TryParse<ProviderType>(provider, true, out var parsed) ? parsed : null;
    }

    private static void RegisterRemoteFailure()
    {
        long retryAt = DateTimeOffset.UtcNow.Add(RemoteFailureCooldown).ToUnixTimeMilliseconds();
        Interlocked.Exchange(ref _remoteRetryAfterUtcMs, retryAt);
    }

    private static void ResetRemoteFailureState()
    {
        Interlocked.Exchange(ref _remoteRetryAfterUtcMs, 0);
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public static readonly NullHttpClientFactory Instance = new();

        private readonly HttpClient _client = new();

        private NullHttpClientFactory()
        {
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    public Task<ConversationMetadataRecord> GetConversationAsync(
        string conversationId,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        IQueryable<ConversationMetadataRecord> query = _dbContext.Conversations;

        if (includeDetails)
        {
            query = query
                .Include(record => record.RuntimeInstance)
                    .ThenInclude(runtime => runtime!.Hosts)
                .Include(record => record.RuntimeInstance)
                    .ThenInclude(runtime => runtime!.Providers)
                .Include(record => record.Microagents)
                .Include(record => record.Files)
                .Include(record => record.GitDiffs)
                .Include(record => record.FeedbackEntries)
                .Include(record => record.RememberPrompts);
        }

        return query.FirstOrDefaultAsync(
            record => record.ConversationId == conversationId,
            cancellationToken);
    }

    public async Task<ConversationMetadataRecord> CreateConversationAsync(
        ConversationMetadataRecord record,
        CancellationToken cancellationToken)
    {
        await _dbContext.Conversations.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task<bool> DeleteConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        ConversationMetadataRecord record = await _dbContext.Conversations
            .FirstOrDefaultAsync(conversation => conversation.ConversationId == conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return false;
        }

        _dbContext.Conversations.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> GetNextEventIdAsync(Guid conversationRecordId, CancellationToken cancellationToken)
    {
        int? currentMax = await _dbContext.ConversationEvents
            .Where(evt => evt.ConversationMetadataRecordId == conversationRecordId)
            .MaxAsync(evt => (int?)evt.EventId, cancellationToken)
            .ConfigureAwait(false);

        return (currentMax ?? 0) + 1;
    }

    public async Task<ConversationEventRecord> AddEventAsync(
        ConversationEventRecord record,
        CancellationToken cancellationToken)
    {
        await _dbContext.ConversationEvents.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task<IReadOnlyList<ConversationEventRecord>> GetEventsAsync(
        Guid conversationRecordId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ConversationEvents
            .Where(evt => evt.ConversationMetadataRecordId == conversationRecordId)
            .OrderBy(evt => evt.EventId)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConversationEventRecord>> GetEventsAsync(
        Guid conversationRecordId,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<ConversationEventRecord> query = _dbContext.ConversationEvents
            .Where(evt => evt.ConversationMetadataRecordId == conversationRecordId);

        if (reverse)
        {
            query = query.OrderByDescending(evt => evt.EventId);

            if (startId > 0)
            {
                query = query.Where(evt => evt.EventId <= startId);
            }

            if (endId.HasValue)
            {
                query = query.Where(evt => evt.EventId >= endId.Value);
            }
        }
        else
        {
            query = query.OrderBy(evt => evt.EventId);

            if (startId > 0)
            {
                query = query.Where(evt => evt.EventId >= startId);
            }

            if (endId.HasValue)
            {
                query = query.Where(evt => evt.EventId <= endId.Value);
            }
        }

        return await query
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ConversationRememberPromptRecord> GetRememberPromptAsync(
        Guid conversationRecordId,
        int eventId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ConversationRememberPrompts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                prompt => prompt.ConversationMetadataRecordId == conversationRecordId && prompt.EventId == eventId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetRememberPromptAsync(
        Guid conversationRecordId,
        int eventId,
        string prompt,
        CancellationToken cancellationToken)
    {
        ConversationRememberPromptRecord existing = await _dbContext.ConversationRememberPrompts
            .FirstOrDefaultAsync(
                record => record.ConversationMetadataRecordId == conversationRecordId && record.EventId == eventId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new ConversationRememberPromptRecord
            {
                Id = Guid.NewGuid(),
                ConversationMetadataRecordId = conversationRecordId,
                EventId = eventId,
                Prompt = prompt
            };

            await _dbContext.ConversationRememberPrompts.AddAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Prompt = prompt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
