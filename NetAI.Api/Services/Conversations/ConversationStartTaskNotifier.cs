using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;

public class ConversationStartTaskNotifier
{
    private static readonly UnboundedChannelOptions ChannelOptions = new()
    {
        SingleReader = false,
        SingleWriter = false,
    };

    private readonly ConcurrentDictionary<Guid, List<Channel<AppConversationStartTaskDto>>> _subscribers = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };
    private readonly ILogger<ConversationStartTaskNotifier> _logger;

    public ConversationStartTaskNotifier(ILogger<ConversationStartTaskNotifier> logger)
    {
        _logger = logger;
    }

    public AppConversationStartTaskDto ToDto(ConversationStartTaskRecord record)
    {
        AppConversationStartRequestDto request = null;
        try
        {
            request = JsonSerializer.Deserialize<AppConversationStartRequestDto>(record.RequestJson ?? "{}", _serializerOptions);
        }
        catch (JsonException)
        {
            request = new AppConversationStartRequestDto();
        }

        request ??= new AppConversationStartRequestDto();

        string status = ToStatusString(record.Status);
        if (string.Equals(status, "READY", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(record.AppConversationId))
        {
            _logger.LogWarning(
                "Start task {TaskId} reported READY without conversation id. Downgrading to STARTING_CONVERSATION. Status: {Status}; Detail: {Detail}; RuntimeStatus: {RuntimeStatus}; BackendError: {BackendError}",
                record.Id,
                record.Status,
                record.Detail,
                record.RuntimeStatus,
                record.BackendError);
            status = "STARTING_CONVERSATION";
        }

        _logger.LogInformation(
            "Start task {TaskId} mapped to DTO with status {Status}. ConversationId: {ConversationId}; SandboxId: {SandboxId}; ConversationStatus: {ConversationStatus}; RuntimeStatus: {RuntimeStatus}",
            record.Id,
            status,
            record.AppConversationId,
            record.SandboxId,
            record.ConversationStatus,
            record.RuntimeStatus);

        return new AppConversationStartTaskDto
        {
            Id = record.Id,
            CreatedByUserId = record.CreatedByUserId,
            Status = status,
            Detail = record.Detail,
            FailureDetail = record.FailureDetail,
            AppConversationId = record.AppConversationId,
            SandboxId = record.SandboxId,
            AgentServerUrl = record.AgentServerUrl,
            SandboxSessionApiKey = record.SandboxSessionApiKey,
            SandboxWorkspacePath = record.SandboxWorkspacePath,
            SandboxVscodeUrl = record.SandboxVscodeUrl,
            ConversationStatus = record.ConversationStatus,
            RuntimeStatus = record.RuntimeStatus,
            BackendError = record.BackendError,
            Request = request,
            CreatedAt = record.CreatedAtUtc,
            UpdatedAt = record.UpdatedAtUtc,
        };
    }

    public ChannelReader<AppConversationStartTaskDto> Subscribe(Guid taskId, AppConversationStartTaskDto initial)
    {
        Channel<AppConversationStartTaskDto> channel = Channel.CreateUnbounded<AppConversationStartTaskDto>(ChannelOptions);
        channel.Writer.TryWrite(initial);

        List<Channel<AppConversationStartTaskDto>> list = _subscribers.GetOrAdd(taskId, _ => new List<Channel<AppConversationStartTaskDto>>());
        lock (list)
        {
            list.Add(channel);
        }

        return channel.Reader;
    }

    public void Publish(ConversationStartTaskRecord record)
    {
        Publish(ToDto(record));
    }

    public void Publish(AppConversationStartTaskDto dto)
    {
        if (!_subscribers.TryGetValue(dto.Id, out List<Channel<AppConversationStartTaskDto>> channels))
        {
            _logger.LogWarning("No subscribers found when publishing start task {TaskId} with status {Status}", dto.Id, dto.Status);
            return;
        }

        bool complete = IsTerminal(dto.Status);
        lock (channels)
        {
            foreach (Channel<AppConversationStartTaskDto> channel in channels)
            {
                _logger.LogInformation(
                    "Publishing start task {TaskId} update with status {Status}. Detail: {Detail}; FailureDetail: {FailureDetail}; ConversationId: {ConversationId}",
                    dto.Id,
                    dto.Status,
                    dto.Detail,
                    dto.FailureDetail,
                    dto.AppConversationId);
                channel.Writer.TryWrite(dto);
                if (complete)
                {
                    channel.Writer.TryComplete();
                }
            }

            if (complete)
            {
                _subscribers.TryRemove(dto.Id, out _);
            }
        }
    }

    public void Cancel(Guid taskId)
    {
        if (!_subscribers.TryRemove(taskId, out List<Channel<AppConversationStartTaskDto>> channels))
        {
            return;
        }

        foreach (Channel<AppConversationStartTaskDto> channel in channels)
        {
            channel.Writer.TryComplete();
        }
    }

    //todo est

    private static string ToStatusString(ConversationStartTaskStatus status)
    {
        return status switch
        {
            ConversationStartTaskStatus.Working => "WORKING",
            ConversationStartTaskStatus.WaitingForSandbox => "WAITING_FOR_SANDBOX",
            ConversationStartTaskStatus.PreparingRepository => "PREPARING_REPOSITORY",
            ConversationStartTaskStatus.RunningSetupScript => "RUNNING_SETUP_SCRIPT",
            ConversationStartTaskStatus.SettingUpGitHooks => "SETTING_UP_GIT_HOOKS",
            ConversationStartTaskStatus.StartingConversation => "STARTING_CONVERSATION",
            ConversationStartTaskStatus.Ready => "READY",
            ConversationStartTaskStatus.Error => "ERROR",
            _ => status.ToString().ToUpperInvariant(),
        };
    }

    private static bool IsTerminal(string status)
    {
        return string.Equals(status, "READY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase);
    }
}
