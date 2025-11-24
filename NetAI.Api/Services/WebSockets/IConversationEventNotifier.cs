using System.Threading.Channels;

namespace NetAI.Api.Services.WebSockets;

public interface IConversationEventNotifier
{
    ConversationEventSubscription Subscribe(string conversationId, CancellationToken cancellationToken);

    Task PublishAsync(string conversationId, string payloadJson, CancellationToken cancellationToken);
}

public sealed class ConversationEventSubscription : IAsyncDisposable
{
    private readonly Channel<string> _channel;
    private readonly Action _onDispose;
    private bool _disposed;

    public ConversationEventSubscription(Channel<string> channel, Action onDispose)
    {
        _channel = channel;
        _onDispose = onDispose;
    }

    public ChannelReader<string> Reader => _channel.Reader;

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _channel.Writer.TryComplete();
        _onDispose();
        return ValueTask.CompletedTask;
    }
}
