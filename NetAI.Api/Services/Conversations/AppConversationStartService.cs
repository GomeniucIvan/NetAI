using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Conversations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetAI.Api.Services.Conversations;

public class AppConversationStartService : IAppConversationStartService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IConversationStartTaskRepository _repository;
    private readonly ConversationStartTaskQueue _queue;
    private readonly ConversationStartTaskNotifier _notifier;
    private readonly ILogger<AppConversationStartService> _logger;
    private readonly ConversationStartTaskOptions _options;

    public AppConversationStartService(
        IConversationStartTaskRepository repository,
        ConversationStartTaskQueue queue,
        ConversationStartTaskNotifier notifier,
        IOptions<ConversationStartTaskOptions> options,
        ILogger<AppConversationStartService> logger)
    {
        _repository = repository;
        _queue = queue;
        _notifier = notifier;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AppConversationStartTaskDto> StartAsync(AppConversationStartRequestDto request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Received conversation start request. Repository={Repository}; Branch={Branch}; GitProvider={GitProvider}; Title={Title}",
            request?.SelectedRepository,
            request?.SelectedBranch,
            request?.GitProvider,
            request?.Title);
        AppConversationStartTaskDto dto = await CreateTaskAsync(request, cancellationToken).ConfigureAwait(false);
        await _queue.EnqueueAsync(dto.Id, cancellationToken).ConfigureAwait(false);
        return dto;
    }

    public async IAsyncEnumerable<AppConversationStartTaskDto> StreamStartAsync(AppConversationStartRequestDto request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AppConversationStartTaskDto initial = await StartAsync(request, cancellationToken).ConfigureAwait(false);
        ChannelReader<AppConversationStartTaskDto> reader = _notifier.Subscribe(initial.Id, initial);
        _logger.LogInformation(
            "Subscribed to task {TaskId} stream with initial status {Status}",
            initial.Id,
            initial.Status);

        await foreach (AppConversationStartTaskDto update in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "Streaming update for task {TaskId}: Status={Status}; Detail={Detail}; ConversationId={ConversationId}",
                update.Id,
                update.Status,
                update.Detail,
                update.AppConversationId);
            yield return update;
        }
    }

    public async Task<IReadOnlyList<AppConversationStartTaskDto>> BatchGetAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ConversationStartTaskRecord> records = await _repository.BatchGetAsync(ids, cancellationToken).ConfigureAwait(false);
        return records.Select(record => record is null ? null : _notifier.ToDto(record)).ToList();
    }

    public async Task<AppConversationStartTaskPageDto> SearchAsync(int limit, string pageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CleanupAsync(cancellationToken).ConfigureAwait(false);

        int effectiveLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 100);
        (IReadOnlyList<ConversationStartTaskRecord> Items, string NextPageId) result = await _repository
            .SearchAsync(pageId, effectiveLimit, cancellationToken)
            .ConfigureAwait(false);

        List<AppConversationStartTaskDto> items = result.Items
            .Select(_notifier.ToDto)
            .ToList();

        return new AppConversationStartTaskPageDto
        {
            Items = items,
            NextPageId = result.NextPageId,
        };
    }

    public Task<int> CountAsync(Guid? conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string identifier = conversationId?.ToString();
        return _repository.CountAsync(identifier, cancellationToken);
    }

    internal async Task<AppConversationStartTaskDto> CreateTaskAsync(AppConversationStartRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new AppConversationStartRequestDto();

        var record = new ConversationStartTaskRecord
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = request.CreatedByUserId,
            Status = ConversationStartTaskStatus.Working,
            Detail = "Queued conversation start",
            RequestJson = SerializeRequest(request),
        };

        ConversationStartTaskRecord persisted = await _repository.AddAsync(record, cancellationToken).ConfigureAwait(false);
        AppConversationStartTaskDto dto = _notifier.ToDto(persisted);
        _notifier.Publish(dto);
        _logger.LogInformation("Queued conversation start task {TaskId}", dto.Id);
        return dto;
    }

    internal void Publish(ConversationStartTaskRecord record)
    {
        _notifier.Publish(record);
    }

    private static string SerializeRequest(AppConversationStartRequestDto request)
    {
        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        TimeSpan retention = _options.CompletedTaskRetention <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(5)
            : _options.CompletedTaskRetention;

        DateTimeOffset threshold = DateTimeOffset.UtcNow - retention;
        int removed = await _repository.CleanupCompletedAsync(threshold, cancellationToken).ConfigureAwait(false);
        if (removed > 0)
        {
            _logger.LogDebug("Removed {Count} completed conversation start tasks", removed);
        }
    }
}
