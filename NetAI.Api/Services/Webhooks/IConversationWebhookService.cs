using NetAI.Api.Models.Webhooks;

namespace NetAI.Api.Services.Webhooks;

public interface IConversationWebhookService
{
    Task UpsertConversationAsync(
        string sandboxId,
        string sessionApiKey,
        WebhookConversationDto conversation,
        CancellationToken cancellationToken);
}
