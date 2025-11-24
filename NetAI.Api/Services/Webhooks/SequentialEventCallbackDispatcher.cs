using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Webhooks;

namespace NetAI.Api.Services.Webhooks;

public class SequentialEventCallbackDispatcher : IEventCallbackDispatcher
{
    private readonly IReadOnlyList<IEventCallback> _callbacks;
    private readonly ILogger<SequentialEventCallbackDispatcher> _logger;

    public SequentialEventCallbackDispatcher(
        IEnumerable<IEventCallback> callbacks,
        ILogger<SequentialEventCallbackDispatcher> logger)
    {
        _callbacks = callbacks?.ToList() ?? new List<IEventCallback>();
        _logger = logger;
    }

    public async Task DispatchAsync(
        string conversationId,
        WebhookEventDto webhookEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (IEventCallback callback in _callbacks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await callback.ExecuteAsync(conversationId, webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Event callback {CallbackType} failed for conversation {ConversationId}",
                    callback.GetType().Name,
                    conversationId);
            }
        }
    }
}
