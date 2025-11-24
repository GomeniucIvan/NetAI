using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Webhooks;

public interface IWebhookValidator
{
    Task<SandboxInfoDto> ValidateSandboxAsync(
        string sandboxId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<ConversationMetadataRecord> EnsureConversationAsync(
        Guid conversationId,
        SandboxInfoDto sandbox,
        bool allowCreation,
        CancellationToken cancellationToken);
}
