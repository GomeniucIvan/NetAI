using System;
using Microsoft.EntityFrameworkCore;
using NetAI.Api.Data;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Services.Sandboxes;

namespace NetAI.Api.Services.Webhooks;

public class WebhookValidator : IWebhookValidator
{
    private readonly ISandboxService _sandboxService;
    private readonly NetAiDbContext _dbContext;

    public WebhookValidator(ISandboxService sandboxService, NetAiDbContext dbContext)
    {
        _sandboxService = sandboxService;
        _dbContext = dbContext;
    }

    public async Task<SandboxInfoDto> ValidateSandboxAsync(
        string sandboxId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sandboxId))
        {
            throw new UnauthorizedAccessException("Sandbox identifier is required.");
        }

        SandboxInfoDto sandbox = await _sandboxService
            .GetSandboxAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        if (sandbox is null || string.IsNullOrWhiteSpace(sandbox.SessionApiKey))
        {
            throw new UnauthorizedAccessException("Sandbox not found or inactive.");
        }

        if (!string.Equals(sandbox.SessionApiKey, sessionApiKey, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid session API key.");
        }

        return sandbox;
    }

    public async Task<ConversationMetadataRecord> EnsureConversationAsync(
        Guid conversationId,
        SandboxInfoDto sandbox,
        bool allowCreation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedId = conversationId.ToString("N");
        ConversationMetadataRecord conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(record => record.ConversationId == normalizedId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            if (!allowCreation)
            {
                throw new UnauthorizedAccessException("Conversation not found.");
            }

            conversation = new ConversationMetadataRecord
            {
                Id = Guid.NewGuid(),
                ConversationId = normalizedId,
                SandboxId = sandbox.Id,
                UserId = sandbox.CreatedByUserId,
                CreatedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow
            };

            await _dbContext.Conversations.AddAsync(conversation, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return conversation;
        }

        if (!string.IsNullOrWhiteSpace(conversation.UserId)
            && !string.Equals(conversation.UserId, sandbox.CreatedByUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Conversation belongs to a different user.");
        }

        bool updated = false;

        if (string.IsNullOrWhiteSpace(conversation.UserId) && !string.IsNullOrWhiteSpace(sandbox.CreatedByUserId))
        {
            conversation.UserId = sandbox.CreatedByUserId;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(conversation.SandboxId)
            && !string.Equals(conversation.SandboxId, sandbox.Id, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Conversation is associated with another sandbox.");
        }

        if (string.IsNullOrWhiteSpace(conversation.SandboxId))
        {
            conversation.SandboxId = sandbox.Id;
            updated = true;
        }

        if (updated)
        {
            conversation.LastUpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return conversation;
    }
}
