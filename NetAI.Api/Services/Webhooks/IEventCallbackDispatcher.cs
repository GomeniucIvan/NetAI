using NetAI.Api.Models.Webhooks;

namespace NetAI.Api.Services.Webhooks;

public interface IEventCallbackDispatcher
{
    Task DispatchAsync(
        string conversationId,
        WebhookEventDto webhookEvent,
        CancellationToken cancellationToken);
}
