using System;
using System.Collections.Generic;
using System.Linq;
using NetAI.Api.Data;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Models.Webhooks;

namespace NetAI.Api.Services.Webhooks;

public class ConversationWebhookService : IConversationWebhookService
{
    private readonly IWebhookValidator _validator;
    private readonly NetAiDbContext _dbContext;

    public ConversationWebhookService(IWebhookValidator validator, NetAiDbContext dbContext)
    {
        _validator = validator;
        _dbContext = dbContext;
    }

    public async Task UpsertConversationAsync(
        string sandboxId,
        string sessionApiKey,
        WebhookConversationDto conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        SandboxInfoDto sandbox = await _validator
            .ValidateSandboxAsync(sandboxId, sessionApiKey, cancellationToken)
            .ConfigureAwait(false);

        ConversationMetadataRecord record = await _validator
            .EnsureConversationAsync(conversation.Id, sandbox, allowCreation: true, cancellationToken)
            .ConfigureAwait(false);

        UpdateConversationMetadata(record, sandbox, conversation);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void UpdateConversationMetadata(
        ConversationMetadataRecord record,
        SandboxInfoDto sandbox,
        WebhookConversationDto payload)
    {
        record.SandboxId = sandbox.Id;
        record.UserId = sandbox.CreatedByUserId;
        record.SessionApiKey = sandbox.SessionApiKey;
        record.LastUpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(payload.Title))
        {
            record.Title = payload.Title;
        }

        if (!string.IsNullOrWhiteSpace(payload.SelectedRepository))
        {
            record.SelectedRepository = payload.SelectedRepository;
        }

        if (!string.IsNullOrWhiteSpace(payload.SelectedBranch))
        {
            record.SelectedBranch = payload.SelectedBranch;
        }

        if (!string.IsNullOrWhiteSpace(payload.GitProvider))
        {
            record.GitProviderRaw = payload.GitProvider;
            record.GitProvider = TryParseProvider(payload.GitProvider);
        }

        if (!string.IsNullOrWhiteSpace(payload.Trigger))
        {
            record.Trigger = TryParseTrigger(payload.Trigger);
        }

        if (payload.PullRequestNumbers is not null)
        {
            record.PullRequestNumbers = payload.PullRequestNumbers.ToList();
        }

        string model = payload.Agent?.Llm?.Model;
        if (!string.IsNullOrWhiteSpace(model))
        {
            record.LlmModel = model;
        }
    }

    private static ProviderType? TryParseProvider(string value)
    {
        return Enum.TryParse<ProviderType>(value, ignoreCase: true, out ProviderType provider)
            ? provider
            : null;
    }

    private static ConversationTrigger? TryParseTrigger(string value)
    {
        return Enum.TryParse<ConversationTrigger>(value, ignoreCase: true, out ConversationTrigger trigger)
            ? trigger
            : null;
    }
}
