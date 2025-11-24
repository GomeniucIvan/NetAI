using System.Collections.Concurrent;
using System.Threading.Channels;

namespace NetAI.Api.Services.WebSockets;

public sealed class ConversationEventNotifier : IConversationEventNotifier
{
    private static readonly UnboundedChannelOptions ChannelOptions = new()
    {
        SingleReader = true,
        SingleWriter = false,
    };

    private readonly ConcurrentDictionary<string, List<Channel<string>>> _subscribers = new(StringComparer.OrdinalIgnoreCase);

    public ConversationEventSubscription Subscribe(string conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("Conversation id is required", nameof(conversationId));
        }

        var channel = Channel.CreateUnbounded<string>(ChannelOptions);
        List<Channel<string>> list = _subscribers.GetOrAdd(conversationId, _ => new List<Channel<string>>());

        lock (list)
        {
            list.Add(channel);
        }

        cancellationToken.Register(() => RemoveChannel(conversationId, channel));

        return new ConversationEventSubscription(channel, () => RemoveChannel(conversationId, channel));
    }

    public Task PublishAsync(string conversationId, string payloadJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(payloadJson))
        {
            return Task.CompletedTask;
        }

        if (!_subscribers.TryGetValue(conversationId, out List<Channel<string>> channels))
        {
            return Task.CompletedTask;
        }

        lock (channels)
        {
            foreach (Channel<string> channel in channels.ToArray())
            {
                if (!channel.Writer.TryWrite(payloadJson))
                {
                    channel.Writer.TryComplete();
                    channels.Remove(channel);
                }
            }

            if (channels.Count == 0)
            {
                _subscribers.TryRemove(conversationId, out _);
            }
        }

        return Task.CompletedTask;
    }

    private void RemoveChannel(string conversationId, Channel<string> channel)
    {
        if (!_subscribers.TryGetValue(conversationId, out List<Channel<string>> channels))
        {
            return;
        }

        lock (channels)
        {
            channels.Remove(channel);
            channel.Writer.TryComplete();
            if (channels.Count == 0)
            {
                _subscribers.TryRemove(conversationId, out _);
            }
        }
    }
}
