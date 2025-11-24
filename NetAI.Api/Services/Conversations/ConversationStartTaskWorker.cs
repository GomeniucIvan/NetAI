using System.Text.Json;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Services.Sandboxes;

namespace NetAI.Api.Services.Conversations;

public class ConversationStartTaskWorker : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string AgentServerName = "AGENT_SERVER";
    private const string VscodeName = "VSCODE";

    private readonly ConversationStartTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConversationStartTaskNotifier _notifier;
    private readonly ILogger<ConversationStartTaskWorker> _logger;

    public ConversationStartTaskWorker(
        ConversationStartTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ConversationStartTaskNotifier notifier,
        ILogger<ConversationStartTaskWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid taskId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessTaskAsync(taskId, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing conversation start task {TaskId}", taskId);
            }
        }
    }

    private async Task ProcessTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConversationStartTaskRepository>();
        var sandboxService = scope.ServiceProvider.GetRequiredService<ISandboxService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<IConversationSessionService>();

        ConversationStartTaskRecord record = await repository.GetAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            _logger.LogWarning("Conversation start task {TaskId} was not found", taskId);
            return;
        }

        _logger.LogInformation(
            "Loaded conversation start task {TaskId} with status {Status}. ConversationId: {ConversationId}; SandboxId: {SandboxId}",
            taskId,
            record.Status,
            record.AppConversationId,
            record.SandboxId);

        AppConversationStartRequestDto request = DeserializeRequest(record.RequestJson);
        _logger.LogInformation(
            "Task {TaskId} request summary: Repository={Repository}; Branch={Branch}; GitProvider={GitProvider}; Title={Title}",
            taskId,
            request.SelectedRepository,
            request.SelectedBranch,
            request.GitProvider,
            request.Title);

        try
        {
            await UpdateStatusAsync(record, ConversationStartTaskStatus.WaitingForSandbox, "Provisioning sandbox", repository, cancellationToken).ConfigureAwait(false);

            SandboxInfoDto sandboxInfo = null;

            if (string.IsNullOrWhiteSpace(record.SandboxId))
            {
                try
                {
                    SandboxInfoDto sandbox = await sandboxService.StartSandboxAsync(
                        new StartSandboxRequestDto
                        {
                            CreatedByUserId = request.CreatedByUserId,
                            SandboxSpecId = null,
                        },
                        cancellationToken).ConfigureAwait(false);

                    record.SandboxId = sandbox.Id;

                    //TODO config-json
                    string agentServerUrl = ResolveExposedUrl(sandbox, AgentServerName)
                                            ?? sandbox.RuntimeUrl
                                            ?? $"http://127.0.0.1:7250";

                    record.AgentServerUrl = agentServerUrl;
                    record.SandboxSessionApiKey = sandbox.SessionApiKey;
                    record.SandboxWorkspacePath = sandbox.WorkspacePath;
                    record.SandboxVscodeUrl = ResolveExposedUrl(sandbox, VscodeName);
                    sandboxInfo = sandbox;
                    await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
                    _notifier.Publish(record);
                    _logger.LogInformation(
                        "Provisioned sandbox {SandboxId} for task {TaskId}. AgentServerUrl={AgentServerUrl}; VscodeUrl={VscodeUrl}",
                        record.SandboxId,
                        taskId,
                        record.AgentServerUrl,
                        record.SandboxVscodeUrl);
                }
                catch (SandboxOrchestrationException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Sandbox orchestration unavailable for task {TaskId}. Continuing without sandbox.",
                        taskId);

                    record.SandboxId = null;
                    record.AgentServerUrl = null;
                    record.SandboxSessionApiKey = null;
                    record.SandboxWorkspacePath = null;
                    record.SandboxVscodeUrl = null;
                    record.Detail = "Sandbox unavailable. Continuing without sandbox.";
                    await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
                    _notifier.Publish(record);
                }
            }

            await UpdateStatusAsync(record, ConversationStartTaskStatus.PreparingRepository, "Preparing repository", repository, cancellationToken).ConfigureAwait(false);
            await UpdateStatusAsync(record, ConversationStartTaskStatus.RunningSetupScript, "Running setup scripts", repository, cancellationToken).ConfigureAwait(false);
            await UpdateStatusAsync(record, ConversationStartTaskStatus.SettingUpGitHooks, "Configuring git hooks", repository, cancellationToken).ConfigureAwait(false);

            if (sandboxInfo is null && !string.IsNullOrWhiteSpace(record.SandboxId))
            {
                sandboxInfo = await sandboxService.GetSandboxAsync(record.SandboxId, cancellationToken).ConfigureAwait(false);
            }

            if (sandboxInfo is not null)
            {
                bool updated = false;
                string agentFromSandbox = ResolveExposedUrl(sandboxInfo, AgentServerName) ?? sandboxInfo.RuntimeUrl;
                string vscodeFromSandbox = ResolveExposedUrl(sandboxInfo, VscodeName);

                if (string.IsNullOrWhiteSpace(record.AgentServerUrl) && !string.IsNullOrWhiteSpace(agentFromSandbox))
                {
                    record.AgentServerUrl = agentFromSandbox;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(record.SandboxSessionApiKey) && !string.IsNullOrWhiteSpace(sandboxInfo.SessionApiKey))
                {
                    record.SandboxSessionApiKey = sandboxInfo.SessionApiKey;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(record.SandboxWorkspacePath) && !string.IsNullOrWhiteSpace(sandboxInfo.WorkspacePath))
                {
                    record.SandboxWorkspacePath = sandboxInfo.WorkspacePath;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(record.SandboxVscodeUrl) && !string.IsNullOrWhiteSpace(vscodeFromSandbox))
                {
                    record.SandboxVscodeUrl = vscodeFromSandbox;
                    updated = true;
                }

                if (updated)
                {
                    await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
                    _notifier.Publish(record);
                    _logger.LogInformation(
                        "Refreshed sandbox connection details for task {TaskId}. AgentServerUrl={AgentServerUrl}; WorkspacePath={WorkspacePath}",
                        taskId,
                        record.AgentServerUrl,
                        record.SandboxWorkspacePath);
                }
            }

            CreateConversationRequestDto conversationRequest = BuildConversationRequest(
                request,
                record.SandboxId,
                sandboxInfo,
                record.AgentServerUrl,
                record.SandboxVscodeUrl);
            ConversationDto conversation = await conversationService.CreateConversationAsync(conversationRequest, cancellationToken).ConfigureAwait(false);

            record.AppConversationId = conversation.ConversationId;
            await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            _notifier.Publish(record);
            _logger.LogInformation(
                "Created conversation {ConversationId} for start task {TaskId}",
                conversation.ConversationId,
                taskId);

            await UpdateStatusAsync(record, ConversationStartTaskStatus.StartingConversation, "Starting conversation runtime", repository, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> providers = ResolveProviderSet(request);
            ConversationResponseDto startResponse = await conversationService
                .StartConversationAsync(conversation.ConversationId, conversation.SessionApiKey, providers, cancellationToken)
                .ConfigureAwait(false);

            if (startResponse is null)
            {
                _logger.LogWarning(
                    "Conversation runtime start returned no payload for task {TaskId}, conversation {ConversationId}.",
                    taskId,
                    conversation.ConversationId);
            }
            else
            {
                _logger.LogInformation(
                    "Conversation runtime start response for task {TaskId}, conversation {ConversationId}: Status={Status}; ConversationStatus={ConversationStatus}; RuntimeStatus={RuntimeStatus}; Message={Message}",
                    taskId,
                    conversation.ConversationId,
                    startResponse.Status ?? "<null>",
                    startResponse.ConversationStatus ?? "<null>",
                    startResponse.RuntimeStatus ?? "<null>",
                    string.IsNullOrWhiteSpace(startResponse.Message) ? "<none>" : startResponse.Message);
            }

            if (startResponse is null || !string.Equals(startResponse.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                string errorMessage = startResponse?.Message ?? "Failed to start conversation runtime";
                record.BackendError = errorMessage;
                await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
                _notifier.Publish(record);
                _logger.LogWarning(
                    "Conversation runtime failed to start for task {TaskId}. ConversationId={ConversationId}; Error={Error}",
                    taskId,
                    conversation.ConversationId,
                    errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            record.ConversationStatus = startResponse.ConversationStatus;
            record.RuntimeStatus = startResponse.RuntimeStatus;
            record.BackendError = null;
            await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            _notifier.Publish(record);

            _logger.LogInformation(
                "Recorded runtime start outcome for task {TaskId}. ConversationStatus={ConversationStatus}; RuntimeStatus={RuntimeStatus}; AgentServerUrl={AgentServerUrl}; SandboxId={SandboxId}",
                taskId,
                record.ConversationStatus ?? "<null>",
                record.RuntimeStatus ?? "<null>",
                record.AgentServerUrl ?? "<null>",
                record.SandboxId ?? "<null>");

            record.Status = ConversationStartTaskStatus.Ready;
            record.Detail = "Conversation ready";
            record.FailureDetail = null;
            record.CompletedAtUtc = DateTimeOffset.UtcNow;
            await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            _notifier.Publish(record);

            _logger.LogInformation(
                "Task {TaskId} marked READY with conversation {ConversationId}. ConversationStatus={ConversationStatus}; RuntimeStatus={RuntimeStatus}",
                taskId,
                conversation.ConversationId,
                record.ConversationStatus,
                record.RuntimeStatus);

            _logger.LogInformation(
                "Conversation start task {TaskId} completed successfully with conversation {ConversationId}",
                taskId,
                conversation.ConversationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            record.Status = ConversationStartTaskStatus.Error;
            record.Detail = "Failed to start conversation";
            record.FailureDetail = ex.Message;
            record.BackendError ??= ex.Message;
            record.CompletedAtUtc = DateTimeOffset.UtcNow;
            await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            _notifier.Publish(record);
            _logger.LogError(
                ex,
                "Conversation start task {TaskId} failed. ConversationId={ConversationId}; SandboxId={SandboxId}; Status={Status}",
                taskId,
                record.AppConversationId,
                record.SandboxId,
                record.Status);
        }
    }

    private static AppConversationStartRequestDto DeserializeRequest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppConversationStartRequestDto();
        }

        try
        {
            return JsonSerializer.Deserialize<AppConversationStartRequestDto>(json, SerializerOptions) ?? new AppConversationStartRequestDto();
        }
        catch (JsonException)
        {
            return new AppConversationStartRequestDto();
        }
    }

    private static IReadOnlyList<string> ResolveProviderSet(AppConversationStartRequestDto request)
    {
        if (request.Processors is JsonElement processors && processors.ValueKind == JsonValueKind.Object)
        {
            if (processors.TryGetProperty("providers_set", out JsonElement providersElement)
                && providersElement.ValueKind == JsonValueKind.Array)
            {
                var providers = new List<string>();
                foreach (JsonElement provider in providersElement.EnumerateArray())
                {
                    if (provider.ValueKind == JsonValueKind.String)
                    {
                        string value = provider.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            providers.Add(value);
                        }
                    }
                }

                if (providers.Count > 0)
                {
                    return providers;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.GitProvider))
        {
            return new List<string> { request.GitProvider! };
        }

        return Array.Empty<string>();
    }

    private async Task UpdateStatusAsync(
        ConversationStartTaskRecord record,
        ConversationStartTaskStatus status,
        string detail,
        IConversationStartTaskRepository repository,
        CancellationToken cancellationToken)
    {
        record.Status = status;
        record.Detail = detail;
        if (status != ConversationStartTaskStatus.Error)
        {
            record.FailureDetail = null;
        }

        await repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        _notifier.Publish(record);
        _logger.LogInformation(
            "Updated task {TaskId} to status {Status}. Detail={Detail}",
            record.Id,
            status,
            detail);
    }

    private static CreateConversationRequestDto BuildConversationRequest(
        AppConversationStartRequestDto request,
        string sandboxId,
        SandboxInfoDto sandbox,
        string agentServerUrl,
        string vscodeUrl)
    {
        SandboxConnectionInfoDto connection = null;
        if (sandbox is not null)
        {
            connection = new SandboxConnectionInfoDto
            {
                AgentServerUrl = agentServerUrl ?? sandbox.RuntimeUrl,
                SessionApiKey = sandbox.SessionApiKey,
                VscodeUrl = vscodeUrl ?? ResolveExposedUrl(sandbox, VscodeName),
                WorkspacePath = sandbox.WorkspacePath,
                RuntimeId = sandbox.RuntimeId,
                RuntimeUrl = sandbox.RuntimeUrl,
                RuntimeHosts = sandbox.RuntimeHosts
            };
        }

        return new CreateConversationRequestDto
        {
            Repository = request.SelectedRepository,
            SelectedBranch = request.SelectedBranch,
            GitProvider = request.GitProvider,
            InitialUserMessage = ExtractInitialMessage(request.InitialMessage),
            ConversationInstructions = request.Title,
            PullRequestNumbers = request.PullRequestNumbers?.ToList(),
            Title = request.Title,
            SandboxId = sandboxId,
            SandboxConnection = connection,
        };
    }

    private static string ResolveExposedUrl(SandboxInfoDto sandbox, string name)
    {
        if (sandbox.ExposedUrls is null)
        {
            return null;
        }

        return sandbox.ExposedUrls
            .FirstOrDefault(url => string.Equals(url.Name, name, StringComparison.OrdinalIgnoreCase))?
            .Url;
    }

    private static string ExtractInitialMessage(AppConversationMessageDto message)
    {
        if (message?.Content is null)
        {
            return null;
        }

        foreach (AppConversationMessageContentDto content in message.Content)
        {
            if (string.Equals(content.Type, "text", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(content.Text))
            {
                return content.Text;
            }
        }

        return null;
    }
}
