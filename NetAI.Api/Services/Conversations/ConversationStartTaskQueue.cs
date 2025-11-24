using System.Threading.Channels;

namespace NetAI.Api.Services.Conversations;

public class ConversationStartTaskQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });
    private readonly ILogger<ConversationStartTaskQueue> _logger;

    public ConversationStartTaskQueue(ILogger<ConversationStartTaskQueue> logger)
    {
        _logger = logger;
    }

    public ChannelReader<Guid> Reader => _queue.Reader;

    public ValueTask EnqueueAsync(Guid taskId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enqueuing conversation start task {TaskId}", taskId);
        return _queue.Writer.WriteAsync(taskId, cancellationToken);
    }
}
