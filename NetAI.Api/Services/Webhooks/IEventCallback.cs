using NetAI.Api.Models.Webhooks;

namespace NetAI.Api.Services.Webhooks;

public interface IEventCallback
{
    Task ExecuteAsync(
        string conversationId,
        WebhookEventDto webhookEvent,
        CancellationToken cancellationToken);
}
