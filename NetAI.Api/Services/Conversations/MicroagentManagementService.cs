using Microsoft.Extensions.Options;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Git;

namespace NetAI.Api.Services.Conversations;

public class ConversationFilterOptions
{
    public int ConversationMaxAgeSeconds { get; set; } = 864000;
}

public interface IMicroagentManagementService
{
    Task<ConversationInfoResultSetDto> GetConversationsAsync(
        string selectedRepository,
        int limit,
        string pageId,
        CancellationToken cancellationToken);
}

public class MicroagentManagementService : IMicroagentManagementService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IPullRequestStatusService _pullRequestStatusService;
    private readonly IOptions<ConversationFilterOptions> _options;
    private readonly ILogger<MicroagentManagementService> _logger;

    public MicroagentManagementService(
        IConversationRepository conversationRepository,
        IPullRequestStatusService pullRequestStatusService,
        IOptions<ConversationFilterOptions> options,
        ILogger<MicroagentManagementService> logger)
    {
        _conversationRepository = conversationRepository;
        _pullRequestStatusService = pullRequestStatusService;
        _options = options;
        _logger = logger;
    }

    public async Task<ConversationInfoResultSetDto> GetConversationsAsync(
        string selectedRepository,
        int limit,
        string pageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedRepository))
        {
            throw new ArgumentException("selectedRepository is required", nameof(selectedRepository));
        }

        cancellationToken.ThrowIfCancellationRequested();

        ConversationInfoResultSetDto searchResult = await _conversationRepository
            .GetConversationsAsync(
                limit,
                pageId,
                selectedRepository,
                ConversationTrigger.MicroagentManagement.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<ConversationInfoDto> filteredByAge = FilterByAge(searchResult.Results);

        var finalResults = new List<ConversationInfoDto>();
        foreach (ConversationInfoDto conversation in filteredByAge)
        {
            if (!string.Equals(conversation.SelectedRepository, selectedRepository, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IReadOnlyList<int> pullRequests = conversation.PullRequestNumbers;
            ProviderType? provider = TryParseProvider(conversation.GitProvider);

            if (pullRequests is { Count: > 0 }
                && !string.IsNullOrWhiteSpace(conversation.SelectedRepository)
                && provider.HasValue)
            {
                int lastPr = pullRequests[^1];
                bool isOpen = await _pullRequestStatusService
                    .IsPullRequestOpenAsync(
                        conversation.SelectedRepository!,
                        lastPr,
                        provider.Value,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!isOpen)
                {
                    _logger.LogDebug(
                        "Skipping conversation {ConversationId} because pull request {Repository}#{PrNumber} is closed.",
                        conversation.ConversationId,
                        conversation.SelectedRepository,
                        lastPr);
                    continue;
                }
            }

            finalResults.Add(conversation);
        }

        var dto = new ConversationInfoResultSetDto
        {
            Results = finalResults.Select(ToDto).ToList(),
            NextPageId = searchResult.NextPageId
        };

        return dto;
    }

    private IEnumerable<ConversationInfoDto> FilterByAge(IEnumerable<ConversationInfoDto> records)
    {
        int maxAgeSeconds = _options.Value.ConversationMaxAgeSeconds;
        if (maxAgeSeconds <= 0)
        {
            return records;
        }

        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        return records.Where(record =>
        {
            TimeSpan age = utcNow - record.CreatedAt;
            return age.TotalSeconds <= maxAgeSeconds;
        });
    }

    private static ConversationInfoDto ToDto(ConversationInfoDto conversation)
    {
        var dto = new ConversationInfoDto
        {
            ConversationId = conversation.ConversationId,
            Title = string.IsNullOrWhiteSpace(conversation.Title)
                ? conversation.ConversationId
                : conversation.Title!,
            LastUpdatedAt = conversation.LastUpdatedAt,
            Status = string.IsNullOrWhiteSpace(conversation.Status)
                ? ConversationStatus.Stopped.ToString()
                : conversation.Status!,
            RuntimeStatus = conversation.RuntimeStatus,
            SelectedRepository = conversation.SelectedRepository,
            SelectedBranch = conversation.SelectedBranch,
            GitProvider = conversation.GitProvider,
            Trigger = conversation.Trigger,
            NumConnections = conversation.NumConnections,
            Url = conversation.Url,
            SessionApiKey = conversation.SessionApiKey,
            CreatedAt = conversation.CreatedAt,
            PullRequestNumbers = conversation.PullRequestNumbers?.ToList() ?? new List<int>(),
            ConversationVersion = conversation.ConversationVersion
        };

        return dto;
    }

    private static ProviderType? TryParseProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return Enum.TryParse<ProviderType>(provider, true, out var parsed) ? parsed : null;
    }
}
