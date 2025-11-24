using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Application;
using NetAI.Api.Data.Entities.Conversations;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Models.Git;
using NetAI.Api.Models.Sandboxes;
using NetAI.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NetAI.Api.Services.Conversations;

public class ConversationSessionService : IConversationSessionService
{
    private const int MaxPageSize = 100;

    private static readonly HashSet<string> FilesToIgnore = new(
        new[]
        {
            ".git/",
            ".DS_Store",
            "node_modules/",
            "__pycache__/",
            "lost+found/",
            ".vscode/"
        },
        StringComparer.Ordinal);

    private static readonly string[] RuntimeReadyTokens =
    {
        "STATUS$READY",
        "STATUS$RUNTIME_STARTED",
        "STATUS$RUNNING",
        "READY",
        "RUNTIME_STARTED",
        "RUNNING",
        "READY_OK",
        "INITIALIZED",
        "BOOT_DONE",
        "OK"
    };

    private static readonly string[] RuntimeStoppedTokens =
    {
        "STATUS$STOPPED",
        "STATUS$PAUSED",
        "STOPPED",
        "PAUSED"
    };

    private readonly IConversationRepository _repository;
    private readonly ILogger<ConversationSessionService> _logger;
    private readonly IRuntimeConversationClient _runtimeClient;
    private readonly IRuntimeConversationGateway _runtimeGateway;
    private readonly Uri _runtimeGatewayBaseUri;
    private readonly IApplicationContext _applicationContext;

    public ConversationSessionService(
        IConversationRepository repository,
        ILogger<ConversationSessionService> logger,
        IRuntimeConversationClient runtimeClient,
        IRuntimeConversationGateway runtimeGateway,
        IOptions<RuntimeConversationGatewayOptions> runtimeGatewayOptions,
        IApplicationContext applicationContext)
    {
        _repository = repository;
        _logger = logger;
        _runtimeClient = runtimeClient;
        _runtimeGateway = runtimeGateway;
        _runtimeGatewayBaseUri = CreateRuntimeGatewayBaseUri(runtimeGatewayOptions?.Value);
        _applicationContext = applicationContext;
    }


    public async Task<ResultSetDto<ConversationDto>> GetConversationsAsync(
        int limit,
        string pageId,
        string selectedRepository,
        string conversationTrigger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit <= 0)
        {
            limit = 20;
        }

        limit = Math.Min(limit, MaxPageSize);

        ConversationInfoResultSetDto resultSet = await _repository
            .GetConversationsAsync(limit, pageId, selectedRepository, conversationTrigger, cancellationToken)
            .ConfigureAwait(false);

        var dto = new ResultSetDto<ConversationDto>
        {
            Results = resultSet.Results.Select(ToDto).ToList(),
            NextPageId = resultSet.NextPageId
        };

        return dto;
    }

    public async Task<ConversationDto> GetConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord record = await _repository
            .GetConversationAsync(conversationId, includeDetails: false, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return ToDto(record);
    }

    public async Task<ConversationDto> CreateConversationAsync(CreateConversationRequestDto request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RuntimeConversationInitResult initResult = await _runtimeGateway
            .InitializeConversationAsync(
                new RuntimeConversationInitRequest
                {
                    Conversation = request,
                    SandboxConnection = request.SandboxConnection,
                    ProviderTokens = request.SandboxConnection is null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : ExtractProviderTokens(request.SandboxConnection)
                },
                cancellationToken)
            .ConfigureAwait(false);

        string conversationId = string.IsNullOrWhiteSpace(initResult.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : initResult.ConversationId;

        DateTime utcNow = DateTime.UtcNow;
        DateTimeOffset nowOffset = DateTimeOffset.UtcNow;

        string title = !string.IsNullOrWhiteSpace(initResult.Title)
            ? initResult.Title!
            : DetermineTitle(request);

        ConversationTrigger trigger = ParseTrigger(initResult.Trigger)
            ?? DetermineTrigger(request);

        ConversationStatus defaultStatus = request.SandboxConnection is null
            ? ConversationStatus.Stopped
            : ConversationStatus.Starting;

        ConversationStatus status = ParseConversationStatus(initResult.ConversationStatus, defaultStatus);
        string runtimeStatus = !string.IsNullOrWhiteSpace(initResult.RuntimeStatus)
            ? initResult.RuntimeStatus!
            : (request.SandboxConnection is not null ? "STATUS$READY" : "STATUS$STOPPED");
        string sessionApiKey = !string.IsNullOrWhiteSpace(initResult.SessionApiKey)
            ? initResult.SessionApiKey!
            : request.SandboxConnection?.SessionApiKey
                ?? Guid.NewGuid().ToString("N");
        string runtimeId = !string.IsNullOrWhiteSpace(initResult.RuntimeId)
            ? initResult.RuntimeId!
            : (!string.IsNullOrWhiteSpace(request.SandboxConnection?.RuntimeId)
                ? request.SandboxConnection!.RuntimeId!
                : $"runtime-{Guid.NewGuid():N}");
        string sessionId = !string.IsNullOrWhiteSpace(initResult.SessionId)
            ? initResult.SessionId!
            : $"session-{Guid.NewGuid():N}";
        string runtimeUrl = BuildConversationUrl(
            initResult.RuntimeUrl,
            request.SandboxConnection,
            conversationId);
        string vscodeUrl = initResult.VscodeUrl
            ?? request.SandboxConnection?.VscodeUrl
            ?? $"https://vscode.local/{conversationId}";
        string conversationVersion = DetermineConversationVersion(request);

        var metadata = new ConversationMetadataRecord
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Title = title,
            SelectedRepository = request.Repository,
            SelectedBranch = request.SelectedBranch,
            GitProviderRaw = request.GitProvider,
            GitProvider = TryParseProvider(request.GitProvider),
            SandboxId = request.SandboxId,
            CreatedAtUtc = utcNow,
            LastUpdatedAtUtc = utcNow,
            Trigger = trigger,
            Status = status,
            RuntimeStatus = runtimeStatus,
            Url = runtimeUrl,
            SessionApiKey = sessionApiKey,
            RuntimeId = runtimeId,
            SessionId = sessionId,
            PullRequestNumbers = request.PullRequestNumbers?.ToList() ?? new List<int>(),
            ConversationVersion = conversationVersion,
            VscodeUrl = vscodeUrl
        };

        metadata.Status = DeriveConversationStatus(metadata);

        metadata.RuntimeInstance = new ConversationRuntimeInstanceRecord
        {
            Id = Guid.NewGuid(),
            RuntimeId = metadata.RuntimeId ?? string.Empty,
            SessionId = metadata.SessionId ?? string.Empty,
            SessionApiKey = metadata.SessionApiKey,
            RuntimeStatus = metadata.RuntimeStatus,
            Status = NormalizeStatus(metadata.Status),
            VscodeUrl = metadata.VscodeUrl,
            CreatedAtUtc = nowOffset
        };

        ApplyRuntimeHosts(metadata.RuntimeInstance, initResult.Hosts);
        ApplyRuntimeProviders(metadata.RuntimeInstance, initResult.Providers);

        if (request.CreateMicroagent is not null)
        {
            metadata.Microagents.Add(new ConversationMicroagentRecord
            {
                Id = Guid.NewGuid(),
                Name = request.CreateMicroagent.Title ?? request.CreateMicroagent.Repo ?? "microagent",
                Type = "repo",
                Content = request.CreateMicroagent.Repo ?? string.Empty,
                TriggersJson = SerializeArray(new[] { "manual" }),
                InputsJson = SerializeArray(Array.Empty<InputMetadataDto>()),
                ToolsJson = SerializeArray(Array.Empty<string>())
            });
        }
        PopulateInitialEvents(metadata, initResult.Events, request, nowOffset);

        ConversationMetadataRecord persisted = await _repository
            .CreateConversationAsync(metadata, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Created conversation {ConversationId} with trigger {Trigger}",
            conversationId,
            trigger);

        return ToDto(persisted);
    }

    public async Task<bool> UpdateConversationAsync(string conversationId, UpdateConversationRequestDto request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await _repository
            .GetConversationAsync(conversationId, includeDetails: false, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            conversation.Title = request.Title;
        }

        conversation.LastUpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<bool> DeleteConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _repository.DeleteConversationAsync(conversationId, cancellationToken);
    }

    public async Task<ConversationResponseDto> StartConversationAsync(
        string conversationId,
        string sessionApiKey,
        IEnumerable<string> providers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "StartConversationAsync invoked for {ConversationId}. SessionKeyPresent={HasSessionKey}; Providers={Providers}",
            conversationId,
            !string.IsNullOrWhiteSpace(sessionApiKey),
            providers is null ? "<null>" : string.Join(",", providers));

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        _logger.LogInformation(
            "Loaded conversation {ConversationId} with current status {Status} and runtime status {RuntimeStatus}",
            conversation.ConversationId,
            conversation.Status,
            conversation.RuntimeStatus);

        IReadOnlyList<string> providerList = providers?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).ToList()
            ?? new List<string>();

        RuntimeConversationOperationResult result = await _runtimeGateway
            .StartConversationAsync(
                new RuntimeConversationOperationRequest
                {
                    ConversationId = conversation.ConversationId,
                    SessionApiKey = conversation.SessionApiKey,
                    Providers = providerList,
                    ProviderTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess())
        {
            _logger.LogWarning(
                "Runtime failed to start conversation {ConversationId}. Status={Status}; Message={Message}",
                conversationId,
                result.Status,
                result.Message);
            throw new ConversationRuntimeUnavailableException(conversationId, result.Message ?? "Failed to start conversation");
        }

        _logger.LogInformation(
            "Runtime gateway start result for conversation {ConversationId}: Status={Status}; ConversationStatus={ConversationStatus}; RuntimeStatus={RuntimeStatus}; Message={Message}; RuntimeId={RuntimeId}; SessionId={SessionId}; RuntimeUrl={RuntimeUrl}; VscodeUrl={VscodeUrl}; Providers={Providers}; Hosts={HostCount}; Placeholder={IsPlaceholder}",
            conversation.ConversationId,
            result.Status ?? "<null>",
            result.ConversationStatus ?? "<null>",
            result.RuntimeStatus ?? "<null>",
            string.IsNullOrWhiteSpace(result.Message) ? "<none>" : result.Message,
            result.RuntimeId ?? "<null>",
            result.SessionId ?? "<null>",
            result.RuntimeUrl ?? "<null>",
            result.VscodeUrl ?? "<null>",
            result.Providers is { Count: > 0 } ? string.Join(',', result.Providers) : "<none>",
            result.Hosts?.Count ?? 0,
            result.IsPlaceholder);

        IReadOnlyList<string> runtimeProviders = result.Providers is { Count: > 0 }
            ? result.Providers
            : providerList;

        conversation = await PersistRuntimeOperationAsync(
            conversation,
            sessionApiKey,
            result,
            runtimeProviders,
            operationName: "start",
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Conversation {ConversationId} started successfully. ConversationStatus={ConversationStatus}; RuntimeStatus={RuntimeStatus}",
            conversation.ConversationId,
            result.ConversationStatus ?? NormalizeStatus(conversation.Status),
            result.RuntimeStatus ?? conversation.RuntimeStatus);

        return new ConversationResponseDto
        {
            Status = result.Status,
            ConversationId = conversation.ConversationId,
            ConversationStatus = result.ConversationStatus ?? NormalizeStatus(conversation.Status),
            RuntimeStatus = result.RuntimeStatus ?? conversation.RuntimeStatus,
            Message = result.Message
        };
    }

    public async Task<ConversationResponseDto> StopConversationAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);
        RuntimeConversationOperationResult result = await _runtimeGateway
            .StopConversationAsync(
                new RuntimeConversationOperationRequest
                {
                    ConversationId = conversation.ConversationId,
                    SessionApiKey = conversation.SessionApiKey,
                    Providers = Array.Empty<string>(),
                    ProviderTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess())
        {
            throw new ConversationRuntimeUnavailableException(conversationId, result.Message ?? "Failed to stop conversation");
        }

        conversation = await PersistRuntimeOperationAsync(
            conversation,
            sessionApiKey,
            result,
            Array.Empty<string>(),
            operationName: "stop",
            cancellationToken).ConfigureAwait(false);

        return new ConversationResponseDto
        {
            Status = result.Status,
            ConversationId = conversation.ConversationId,
            ConversationStatus = result.ConversationStatus ?? NormalizeStatus(conversation.Status),
            RuntimeStatus = result.RuntimeStatus ?? conversation.RuntimeStatus,
            Message = result.Message
        };
    }

    public async Task<bool> AddMessageAsync(
        string conversationId,
        string sessionApiKey,
        ConversationMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return false;
        }

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: false,
            cancellationToken);

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _runtimeClient
                .SendMessageAsync(handle, request.Message!, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task AddEventAsync(
        string conversationId,
        string sessionApiKey,
        JsonElement eventPayload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: false,
            cancellationToken);

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _runtimeClient.SendEventAsync(handle, eventPayload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ConversationEventsPageDto> GetEventsAsync(
        string conversationId,
        string sessionApiKey,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        bool excludeHidden,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit < 1 || limit > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        ConversationMetadataRecord conversation;

        try
        {
            conversation = await LoadConversationAsync(
                conversationId,
                sessionApiKey,
                requireSessionKey: true,
                includeDetails: false,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConversationNotFoundException) when (startId <= 0)
        {
            _logger.LogWarning(
                "Conversation {ConversationId} not yet available when retrieving events. Returning empty backlog.",
                conversationId);

            return new ConversationEventsPageDto
            {
                Events = new List<ConversationEventDto>(),
                HasMore = false
            };
        }

        RuntimeConversationHandle handle = null;

        try
        {
            handle = await _runtimeClient
                .AttachAsync(conversation, cancellationToken)
                .ConfigureAwait(false);

            ConversationEventsPageDto page = await _runtimeClient
                .GetEventsAsync(handle, startId, endId, reverse, limit, excludeHidden, cancellationToken)
                .ConfigureAwait(false);

            return page;
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(
                ex,
                "Runtime unavailable when retrieving events for conversation {ConversationId}. Returning persisted events only.",
                conversation.ConversationId);

            return await GetPersistedEventsAsync(
                    conversation,
                    startId,
                    endId,
                    reverse,
                    limit,
                    excludeHidden,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (handle is not null)
            {
                try
                {
                    await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
                }
                catch (ConversationRuntimeUnavailableException detachEx)
                {
                    _logger.LogDebug(
                        detachEx,
                        "Runtime unavailable when detaching from conversation {ConversationId} during events fallback.",
                        conversation.ConversationId);
                }
            }
        }
    }

    public async Task<TrajectoryResponseDto> GetTrajectoryAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: false,
            cancellationToken);

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _runtimeClient.GetTrajectoryAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<RuntimeConfigResponseDto> GetRuntimeConfigAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        RuntimeConfigResponseDto liveConfig = await TryFetchFromGatewayAsync(
            conversation,
            "runtime-config",
            (request, token) => _runtimeGateway.GetRuntimeConfigAsync(request, token),
            cancellationToken).ConfigureAwait(false);

        if (liveConfig is not null)
        {
            bool updated = ApplyRuntimeConfigFromGateway(conversation, liveConfig);
            if (updated)
            {
                conversation.LastUpdatedAtUtc = DateTime.UtcNow;
                EnsureRuntimeInstance(conversation).SessionApiKey = conversation.SessionApiKey;
                await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return liveConfig;
        }

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _runtimeClient.GetRuntimeConfigAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<VSCodeUrlResponseDto> GetVSCodeUrlAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: false,
            cancellationToken);

        VSCodeUrlResponseDto liveResponse = await TryFetchFromGatewayAsync(
            conversation,
            "vscode-url",
            (request, token) => _runtimeGateway.GetVSCodeUrlAsync(request, token),
            cancellationToken).ConfigureAwait(false);

        if (liveResponse is not null)
        {
            bool updated = ApplyVscodeUrlFromGateway(conversation, liveResponse);
            if (updated)
            {
                conversation.LastUpdatedAtUtc = DateTime.UtcNow;
                await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return liveResponse;
        }

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _runtimeClient.GetVSCodeUrlAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<WebHostsResponseDto> GetWebHostsAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        WebHostsResponseDto liveHosts = await TryFetchFromGatewayAsync(
            conversation,
            "web-hosts",
            (request, token) => _runtimeGateway.GetWebHostsAsync(request, token),
            cancellationToken).ConfigureAwait(false);

        if (liveHosts is not null)
        {
            bool updated = ApplyRuntimeHostsFromGateway(conversation, liveHosts);
            if (updated)
            {
                //TODO Update?
                //conversation.LastUpdatedAtUtc = DateTime.UtcNow;
                //await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return liveHosts;
        }

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _runtimeClient.GetWebHostsAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> GetSecurityAnalyzerUrlAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _runtimeClient
                .GetSecurityAnalyzerUrlAsync(handle, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<GetMicroagentsResponseDto> GetMicroagentsAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        var microagents = new List<MicroagentDto>();
        RuntimeConversationHandle handle = null;

        try
        {
            handle = await _runtimeClient
                .AttachAsync(conversation, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<MicroagentDto> runtimeMicroagents = await _runtimeClient
                .GetMicroagentsAsync(handle, cancellationToken)
                .ConfigureAwait(false);

            microagents = runtimeMicroagents?.ToList() ?? new List<MicroagentDto>();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(
                ex,
                "Runtime unavailable when retrieving microagents for conversation {ConversationId}",
                conversation.ConversationId);

            microagents = conversation.Microagents
                .Select(ToMicroagentDto)
                .ToList();
        }
        finally
        {
            if (handle is not null)
            {
                await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
            }
        }

        return new GetMicroagentsResponseDto
        {
            Microagents = microagents
        };
    }

    public async Task<GetMicroagentPromptResponseDto> GetRememberPromptAsync(
        string conversationId,
        string sessionApiKey,
        int eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        string prompt = string.Empty;

        ConversationRememberPromptRecord remember = conversation.RememberPrompts
            .FirstOrDefault(entry => entry.EventId == eventId);

        if (remember is null)
        {
            remember = await _repository
                .GetRememberPromptAsync(conversation.Id, eventId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (remember is not null && !string.IsNullOrWhiteSpace(remember.Prompt))
        {
            prompt = remember.Prompt;
        }
        else
        {
            RuntimeConversationHandle handle = null;

            try
            {
                handle = await _runtimeClient
                    .AttachAsync(conversation, cancellationToken)
                    .ConfigureAwait(false);

                ConversationEventsPageDto page = await _runtimeClient
                    .GetEventsAsync(
                        handle,
                        startId: eventId,
                        endId: eventId,
                        reverse: false,
                        limit: 1,
                        excludeHidden: false,
                        cancellationToken)
                    .ConfigureAwait(false);

                ConversationEventDto evt = page.Events
                    .FirstOrDefault(e => e.Id == eventId);

                if (evt is not null)
                {
                    prompt = ExtractRememberPrompt(evt.Event);
                }
            }
            catch (ConversationRuntimeUnavailableException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Runtime unavailable when retrieving remember prompt event {EventId} for conversation {ConversationId}",
                    eventId,
                    conversation.ConversationId);
            }
            finally
            {
                if (handle is not null)
                {
                    await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                await _repository.SetRememberPromptAsync(conversation.Id, eventId, prompt, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return new GetMicroagentPromptResponseDto
        {
            Status = "ok",
            Prompt = prompt
        };
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(
        string conversationId,
        string sessionApiKey,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);
        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            IReadOnlyList<string> rawEntries = await _runtimeClient
                .ListFilesAsync(handle, path, cancellationToken)
                .ConfigureAwait(false);

            List<string> normalized = rawEntries
                .Select(entry => NormalizeFileEntry(path, entry))
                .Where(entry => !FilesToIgnore.Contains(entry))
                .ToList();

            IReadOnlyList<string> filtered = await ApplyGitIgnoreFilterAsync(
                    handle,
                    normalized,
                    cancellationToken)
                .ConfigureAwait(false);

            return filtered
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<FileUploadSuccessResponseDto> UploadFilesAsync(
        string conversationId,
        string sessionApiKey,
        IEnumerable<IFormFile> files,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        var uploaded = new List<string>();
        var skipped = new List<SkippedFileDto>();
        var uploadDescriptors = new List<RuntimeConversationUploadFile>();

        try
        {
            foreach (IFormFile file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (file is null)
                {
                    continue;
                }

                if (file.Length <= 0)
                {
                    skipped.Add(new SkippedFileDto
                    {
                        Name = file.FileName,
                        Reason = "empty"
                    });

                    continue;
                }

                Stream stream = file.OpenReadStream();
                uploadDescriptors.Add(new RuntimeConversationUploadFile
                {
                    FileName = file.FileName,
                    Content = stream,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType
                });
            }

            if (uploadDescriptors.Count > 0)
            {
                string conversationUrl = conversation.Url ?? $"/api/conversations/{conversation.ConversationId}";

                RuntimeConversationUploadResult result;
                try
                {
                    result = await _runtimeGateway
                        .UploadFilesAsync(
                            new RuntimeConversationUploadRequest
                            {
                                ConversationUrl = conversationUrl,
                                SessionApiKey = conversation.SessionApiKey,
                                Files = uploadDescriptors
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (RuntimeConversationGatewayException ex)
                {
                    throw new ConversationRuntimeActionException(
                        conversation.ConversationId,
                        ExtractGatewayError(ex));
                }
                catch (HttpRequestException ex)
                {
                    throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
                }

                foreach (string path in result.UploadedFiles)
                {
                    string name = string.IsNullOrWhiteSpace(path) ? path : Path.GetFileName(path);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (conversation.Files.All(existing => !string.Equals(existing.Path, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        conversation.Files.Add(new ConversationFileRecord
                        {
                            Id = Guid.NewGuid(),
                            ConversationMetadataRecordId = conversation.Id,
                            Path = name
                        });
                    }

                    uploaded.Add(name);
                }

                foreach (RuntimeUploadSkippedFile skippedFile in result.SkippedFiles)
                {
                    if (string.IsNullOrWhiteSpace(skippedFile.Name))
                    {
                        continue;
                    }

                    skipped.Add(new SkippedFileDto
                    {
                        Name = skippedFile.Name,
                        Reason = string.IsNullOrWhiteSpace(skippedFile.Reason) ? "error" : skippedFile.Reason
                    });
                }
            }
        }
        finally
        {
            foreach (RuntimeConversationUploadFile descriptor in uploadDescriptors)
            {
                descriptor.Content.Dispose();
            }
        }

        conversation.LastUpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new FileUploadSuccessResponseDto
        {
            UploadedFiles = uploaded,
            SkippedFiles = skipped
        };
    }

    private static string NormalizeFileEntry(string basePath, string entry)
    {
        bool isDirectory = entry.EndsWith("/", StringComparison.Ordinal);
        string trimmed = isDirectory ? entry.TrimEnd('/') : entry;
        string normalizedEntry = trimmed.Replace('\\', '/');

        if (!string.IsNullOrWhiteSpace(basePath))
        {
            string normalizedBase = basePath.Replace('\\', '/').TrimEnd('/');
            normalizedEntry = string.IsNullOrEmpty(normalizedEntry)
                ? normalizedBase
                : string.IsNullOrEmpty(normalizedBase)
                    ? normalizedEntry
                    : $"{normalizedBase}/{normalizedEntry}";
        }

        if (isDirectory && !normalizedEntry.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedEntry += "/";
        }

        return normalizedEntry;
    }

    private async Task<IReadOnlyList<string>> ApplyGitIgnoreFilterAsync(
        RuntimeConversationHandle handle,
        IReadOnlyList<string> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return entries;
        }

        RuntimeObservation observation;
        try
        {
            observation = await _runtimeClient
                .RunActionAsync(handle, new RuntimeFileReadAction(".gitignore"), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            throw new ConversationRuntimeActionException(
                handle.Conversation.ConversationId,
                $"Error filtering files: {ex.Reason}");
        }
        catch (ConversationRuntimeActionException ex)
        {
            throw new ConversationRuntimeActionException(
                handle.Conversation.ConversationId,
                $"Error filtering files: {ex.Reason}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to apply .gitignore filter for conversation {ConversationId}",
                handle.Conversation.ConversationId);
            return entries;
        }

        if (observation is RuntimeFileReadObservation readObservation
            && !string.IsNullOrWhiteSpace(readObservation.Content))
        {
            GitIgnoreMatcher matcher = GitIgnoreMatcher.Parse(readObservation.Content);
            return entries.Where(entry => !matcher.IsIgnored(entry)).ToList();
        }

        if (observation is RuntimeErrorObservation errorObservation)
        {
            if (!errorObservation.Message.Contains("File not found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Received error when reading .gitignore for conversation {ConversationId}: {Message}",
                    handle.Conversation.ConversationId,
                    errorObservation.Message);
            }

            return entries;
        }

        return entries;
    }

    public async Task<FileSelectionResultDto> SelectFileAsync(
        string conversationId,
        string sessionApiKey,
        string file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            RuntimeObservation observation;
            try
            {
                observation = await _runtimeClient
                    .RunActionAsync(handle, new RuntimeFileReadAction(file), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ConversationRuntimeUnavailableException ex)
            {
                throw new ConversationRuntimeActionException(
                    conversation.ConversationId,
                    $"Error opening file: {ex.Reason}");
            }
            catch (ConversationRuntimeActionException ex)
            {
                throw new ConversationRuntimeActionException(
                    conversation.ConversationId,
                    $"Error opening file: {ex.Reason}");
            }

            if (observation is RuntimeFileReadObservation readObservation)
            {
                return new FileSelectionResultDto
                {
                    Status = FileSelectionStatus.Success,
                    Code = readObservation.Content
                };
            }

            if (observation is RuntimeErrorObservation errorObservation)
            {
                if (errorObservation.Message.Contains("ERROR_BINARY_FILE", StringComparison.OrdinalIgnoreCase))
                {
                    return new FileSelectionResultDto
                    {
                        Status = FileSelectionStatus.Binary,
                        Error = $"Unable to open binary file: {file}"
                    };
                }

                return new FileSelectionResultDto
                {
                    Status = FileSelectionStatus.Error,
                    Error = $"Error opening file: {errorObservation.Message}"
                };
            }

            return new FileSelectionResultDto
            {
                Status = FileSelectionStatus.Error,
                Error = $"Error opening file: Unexpected observation type: {observation.GetType().Name}"
            };
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<WorkspaceZipStreamDto> ZipWorkspaceAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            RuntimeZipStreamResult zipResult;
            try
            {
                zipResult = await _runtimeClient
                    .ZipWorkspaceAsync(handle, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ConversationRuntimeUnavailableException ex)
            {
                throw new ConversationRuntimeActionException(
                    conversation.ConversationId,
                    $"Error zipping workspace: {ex.Reason}");
            }

            return new WorkspaceZipStreamDto
            {
                Content = zipResult.Content,
                FileName = string.IsNullOrWhiteSpace(zipResult.FileName) ? "workspace.zip" : zipResult.FileName,
                ContentType = string.IsNullOrWhiteSpace(zipResult.ContentType) ? "application/zip" : zipResult.ContentType
            };
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class GitIgnoreMatcher
    {
        private readonly List<GitIgnorePattern> _patterns;

        private GitIgnoreMatcher(List<GitIgnorePattern> patterns)
        {
            _patterns = patterns;
        }

        public static GitIgnoreMatcher Parse(string content)
        {
            var patterns = new List<GitIgnorePattern>();
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                {
                    continue;
                }

                bool negate = line.StartsWith('!');
                if (negate)
                {
                    line = line[1..];
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                bool directoryOnly = line.EndsWith('/');
                if (directoryOnly)
                {
                    line = line.TrimEnd('/');
                }

                bool anchored = line.StartsWith('/');
                if (anchored)
                {
                    line = line.TrimStart('/');
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string normalizedPattern = line.Replace('\\', '/');
                Regex regex = BuildRegex(normalizedPattern, anchored, directoryOnly);
                patterns.Add(new GitIgnorePattern(regex, negate));
            }

            return new GitIgnoreMatcher(patterns);
        }

        public bool IsIgnored(string entry)
        {
            if (_patterns.Count == 0)
            {
                return false;
            }

            string normalizedEntry = entry.Replace('\\', '/');
            bool ignored = false;

            foreach (GitIgnorePattern pattern in _patterns)
            {
                if (pattern.IsMatch(normalizedEntry))
                {
                    ignored = !pattern.Negate;
                }
            }

            return ignored;
        }

        private static Regex BuildRegex(string pattern, bool anchored, bool directoryOnly)
        {
            string escaped = Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", "[^/]");

            string suffix = directoryOnly ? "(/.*)?$" : "$";
            string regexPattern = anchored
                ? $"^{escaped}{suffix}"
                : $"(^|.*/){escaped}{suffix}";

            return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        private sealed record GitIgnorePattern(Regex Regex, bool Negate)
        {
            public bool IsMatch(string entry)
            {
                return Regex.IsMatch(entry);
            }
        }
    }

    public async Task<IReadOnlyList<GitChangeDto>> GetGitChangesAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        string conversationUrl = conversation.Url ?? $"/api/conversations/{conversation.ConversationId}";

        try
        {
            var appConfig = _applicationContext.AppConfiguration;

            IReadOnlyList<RuntimeGitChangeResult> remoteChanges = await _runtimeGateway
                .GetGitChangesAsync(
                    new RuntimeConversationGitChangesRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = conversation.SessionApiKey,
                        Host = appConfig.RuntimeServer?.Url
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return remoteChanges
                .Where(change => !string.IsNullOrWhiteSpace(change.Path))
                .Select(change => new GitChangeDto
                {
                    Path = change.Path,
                    Status = string.IsNullOrWhiteSpace(change.Status) ? "M" : change.Status
                })
                .ToList();
        }
        catch (RuntimeConversationGatewayException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    ex,
                    "Git changes unavailable for conversation {ConversationId}; treating as no changes.",
                    conversation.ConversationId);

                return Array.Empty<GitChangeDto>();
            }

            throw new ConversationRuntimeActionException(
                conversation.ConversationId,
                ExtractGatewayError(ex));
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
        }
    }

    public async Task<GitChangeDiffDto> GetGitDiffAsync(
        string conversationId,
        string sessionApiKey,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConversationResourceNotFoundException(conversationId, "<empty path>");
        }

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        string conversationUrl = conversation.Url ?? $"/api/conversations/{conversation.ConversationId}";

        RuntimeGitDiffResult diffResult;
        try
        {
            diffResult = await _runtimeGateway
                .GetGitDiffAsync(
                    new RuntimeConversationGitDiffRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = conversation.SessionApiKey,
                        Path = path
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RuntimeConversationGatewayException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ConversationResourceNotFoundException(
                    conversation.ConversationId,
                    ExtractGatewayError(ex));
            }

            throw new ConversationRuntimeActionException(
                conversation.ConversationId,
                ExtractGatewayError(ex));
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeActionException(conversation.ConversationId, ex.Message);
        }

        if (diffResult is null
            || (string.IsNullOrWhiteSpace(diffResult.Original)
                && string.IsNullOrWhiteSpace(diffResult.Modified)))
        {
            throw new ConversationResourceNotFoundException(conversationId, path);
        }

        return new GitChangeDiffDto
        {
            Original = diffResult.Original,
            Modified = diffResult.Modified
        };
    }

    public async Task<FeedbackResponseDto> SubmitFeedbackAsync(
        string conversationId,
        string sessionApiKey,
        FeedbackDto feedback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = await LoadConversationAsync(
            conversationId,
            sessionApiKey,
            requireSessionKey: true,
            includeDetails: true,
            cancellationToken);

        var trajectory = new List<object>();

        RuntimeConversationHandle handle = await _runtimeClient
            .AttachAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            TrajectoryResponseDto trajectoryResponse = await _runtimeClient
                .GetTrajectoryAsync(handle, cancellationToken)
                .ConfigureAwait(false);

            if (trajectoryResponse.Trajectory is not null)
            {
                foreach (JsonElement evt in trajectoryResponse.Trajectory)
                {
                    trajectory.Add(evt);
                }
            }
        }
        finally
        {
            await _runtimeClient.DetachAsync(handle, cancellationToken).ConfigureAwait(false);
        }

        feedback.Trajectory = trajectory;

        var entry = new ConversationFeedbackRecord
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FeedbackJson = JsonSerializer.Serialize(feedback),
            ConversationMetadataRecordId = conversation.Id
        };

        conversation.FeedbackEntries.Add(entry);

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new FeedbackResponseDto
        {
            StatusCode = StatusCodes.Status200OK,
            Body = new FeedbackBodyResponseDto
            {
                Message = "Feedback captured.",
                FeedbackId = entry.Id.ToString("N"),
                Password = Guid.NewGuid().ToString("N")
            }
        };
    }

    private async Task<ConversationMetadataRecord> LoadConversationAsync(
        string conversationId,
        string sessionApiKey,
        bool requireSessionKey,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        ConversationMetadataRecord conversation = await _repository
            .GetConversationAsync(conversationId, includeDetails, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            throw new ConversationNotFoundException(conversationId);
        }

        EnsureSessionKey(conversationId, conversation.SessionApiKey, sessionApiKey, requireSessionKey);
        return conversation;
    }

    private static void EnsureSessionKey(
        string conversationId,
        string expected,
        string provided,
        bool requireSessionKey)
    {
        if (!requireSessionKey)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        if (string.Equals(expected, provided, StringComparison.Ordinal))
        {
            return;
        }

        throw new ConversationUnauthorizedException(conversationId);
    }

    private static ConversationDto ToDto(ConversationInfoDto conversation)
    {
        ConversationStatus status = ParseConversationStatus(conversation.Status, ConversationStatus.Stopped);
        string normalizedStatus = NormalizeStatus(status);

        DateTimeOffset createdAt = conversation.CreatedAt;
        DateTimeOffset lastUpdated = conversation.LastUpdatedAt ?? createdAt;

        return new ConversationDto
        {
            ConversationId = conversation.ConversationId,
            Title = string.IsNullOrWhiteSpace(conversation.Title) ? conversation.ConversationId : conversation.Title,
            SelectedRepository = conversation.SelectedRepository,
            SelectedBranch = conversation.SelectedBranch,
            GitProvider = conversation.GitProvider,
            CreatedAt = createdAt,
            LastUpdatedAt = lastUpdated,
            Status = normalizedStatus,
            RuntimeStatus = conversation.RuntimeStatus,
            Trigger = conversation.Trigger?.ToLowerInvariant(),
            Url = conversation.Url,
            SessionApiKey = conversation.SessionApiKey,
            PullRequestNumbers = conversation.PullRequestNumbers,
            ConversationVersion = conversation.ConversationVersion
        };
    }

    private static ConversationDto ToDto(ConversationMetadataRecord record)
    {
        ConversationStatus derivedStatus = DeriveConversationStatus(record);
        string status = NormalizeStatus(derivedStatus);
        string runtimeStatus = record.RuntimeStatus;

        DateTimeOffset createdAt = ToDateTimeOffset(record.CreatedAtUtc);
        DateTimeOffset lastUpdated = record.LastUpdatedAtUtc.HasValue
            ? ToDateTimeOffset(record.LastUpdatedAtUtc.Value)
            : createdAt;

        string conversationVersion = DetermineConversationVersion(record);

        return new ConversationDto
        {
            ConversationId = record.ConversationId,
            Title = record.Title,
            SelectedRepository = record.SelectedRepository,
            SelectedBranch = record.SelectedBranch,
            GitProvider = record.GitProviderRaw ?? record.GitProvider?.ToString(),
            CreatedAt = createdAt,
            LastUpdatedAt = lastUpdated,
            Status = status,
            RuntimeStatus = runtimeStatus,
            Trigger = record.Trigger?.ToString()?.ToLowerInvariant(),
            Url = record.Url,
            SessionApiKey = record.SessionApiKey,
            PullRequestNumbers = record.PullRequestNumbers,
            ConversationVersion = conversationVersion
        };
    }

    private async Task<ConversationMetadataRecord> PersistRuntimeOperationAsync(
        ConversationMetadataRecord conversation,
        string sessionApiKey,
        RuntimeConversationOperationResult result,
        IReadOnlyList<string> runtimeProviders,
        string operationName,
        CancellationToken cancellationToken)
    {
        const int MaxAttempts = 3;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ConversationStatus previousStatus = conversation.Status;
            string previousRuntimeStatus = conversation.RuntimeStatus;

            UpdateConversationFromOperation(conversation, result, runtimeProviders);
            LogConversationStatusUpdate(conversation, previousStatus, previousRuntimeStatus, result, operationName);

            try
            {
                await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return conversation;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict while saving {Operation} operation for conversation {ConversationId} (attempt {Attempt}/{MaxAttempts})",
                    operationName,
                    conversation.ConversationId,
                    attempt,
                    MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    throw new ConversationRuntimeUnavailableException(
                        conversation.ConversationId,
                        $"The conversation was updated by another process. Please retry the {operationName} operation.");
                }

                conversation = await LoadConversationAsync(
                    conversation.ConversationId,
                    sessionApiKey,
                    requireSessionKey: true,
                    includeDetails: true,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        throw new ConversationRuntimeUnavailableException(
            conversation.ConversationId,
            $"Failed to persist {operationName} operation after multiple retries.");
    }

    private static string ExtractGatewayError(RuntimeConversationGatewayException exception)
    {
        if (!string.IsNullOrWhiteSpace(exception.ResponseBody))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(exception.ResponseBody);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("error", out JsonElement errorElement)
                    && errorElement.ValueKind == JsonValueKind.String)
                {
                    string error = errorElement.GetString();
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        return error!;
                    }
                }

                return exception.ResponseBody!;
            }
            catch (JsonException)
            {
                return exception.ResponseBody!;
            }
        }

        return exception.Message;
    }

    private static IDictionary<string, string> ExtractProviderTokens(SandboxConnectionInfoDto connection)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<T> TryFetchFromGatewayAsync<T>(
        ConversationMetadataRecord conversation,
        string operation,
        Func<RuntimeConversationMetadataRequest, CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken)
    {
        try
        {
            var appConfig = _applicationContext.AppConfiguration;

            string conversationUrl = EnsureConversationUrl(conversation, appConfig.RuntimeServer?.Url);
            return await fetch(
                    new RuntimeConversationMetadataRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = conversation.SessionApiKey,
                        Host = appConfig.RuntimeServer?.Url
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RuntimeConversationGatewayException ex)
        {
            _logger.LogWarning(
                ex,
                "Runtime gateway failed to fetch {Operation} for conversation {ConversationId}. Falling back to cached data.",
                operation,
                conversation.ConversationId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Runtime gateway unreachable when fetching {Operation} for conversation {ConversationId}. Falling back to cached data.",
                operation,
                conversation.ConversationId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Runtime gateway misconfigured when fetching {Operation} for conversation {ConversationId}. Falling back to cached data.",
                operation,
                conversation.ConversationId);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Runtime gateway timed out when fetching {Operation} for conversation {ConversationId}. Falling back to cached data.",
                operation,
                conversation.ConversationId);
        }

        return default;
    }

    private static bool ApplyRuntimeConfigFromGateway(
        ConversationMetadataRecord conversation,
        RuntimeConfigResponseDto config)
    {
        bool updated = false;
        ConversationRuntimeInstanceRecord runtime = EnsureRuntimeInstance(conversation);

        if (!string.IsNullOrWhiteSpace(config.RuntimeId)
            && !string.Equals(conversation.RuntimeId, config.RuntimeId, StringComparison.Ordinal))
        {
            conversation.RuntimeId = config.RuntimeId;
            runtime.RuntimeId = config.RuntimeId!;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(config.SessionId)
            && !string.Equals(conversation.SessionId, config.SessionId, StringComparison.Ordinal))
        {
            conversation.SessionId = config.SessionId;
            runtime.SessionId = config.SessionId!;
            updated = true;
        }

        return updated;
    }

    private static bool ApplyVscodeUrlFromGateway(
        ConversationMetadataRecord conversation,
        VSCodeUrlResponseDto response)
    {
        if (string.IsNullOrWhiteSpace(response.VscodeUrl))
        {
            return false;
        }

        if (string.Equals(conversation.VscodeUrl, response.VscodeUrl, StringComparison.Ordinal))
        {
            return false;
        }

        conversation.VscodeUrl = response.VscodeUrl;
        ConversationRuntimeInstanceRecord runtime = EnsureRuntimeInstance(conversation);
        runtime.VscodeUrl = response.VscodeUrl;
        return true;
    }

    private static bool ApplyRuntimeHostsFromGateway(
        ConversationMetadataRecord conversation,
        WebHostsResponseDto response)
    {
        ConversationRuntimeInstanceRecord runtime = EnsureRuntimeInstance(conversation);
        bool hadExistingHosts = runtime.Hosts.Count > 0;

        IReadOnlyList<SandboxRuntimeHostDto> hosts = response.Hosts is { Count: > 0 }
            ? response.Hosts
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => new SandboxRuntimeHostDto
                {
                    Name = pair.Key,
                    Url = pair.Value
                })
                .ToList()
            : Array.Empty<SandboxRuntimeHostDto>();

        ApplyRuntimeHosts(runtime, hosts);

        return hadExistingHosts || hosts.Count > 0;
    }

    private static void ApplyRuntimeHosts(
        ConversationRuntimeInstanceRecord runtimeInstance,
        IReadOnlyList<SandboxRuntimeHostDto> hosts)
    {
        runtimeInstance.Hosts.Clear();
        if (hosts is null || hosts.Count == 0)
        {
            return;
        }

        foreach (SandboxRuntimeHostDto host in hosts)
        {
            if (string.IsNullOrWhiteSpace(host.Name) || string.IsNullOrWhiteSpace(host.Url))
            {
                continue;
            }

            runtimeInstance.Hosts.Add(new ConversationRuntimeHostRecord
            {
                Id = Guid.NewGuid(),
                ConversationRuntimeInstanceRecordId = runtimeInstance.Id,
                Name = host.Name!,
                Url = host.Url!
            });
        }
    }

    private static void ApplyRuntimeProviders(
        ConversationRuntimeInstanceRecord runtimeInstance,
        IReadOnlyList<string> providers)
    {
        runtimeInstance.Providers.Clear();
        if (providers is null || providers.Count == 0)
        {
            return;
        }

        foreach (string provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                continue;
            }

            runtimeInstance.Providers.Add(new ConversationRuntimeProviderRecord
            {
                Id = Guid.NewGuid(),
                ConversationRuntimeInstanceRecordId = runtimeInstance.Id,
                Provider = provider
            });
        }
    }

    private static string ExtractRememberPrompt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("message", out JsonElement messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString() ?? string.Empty;
            }

            if (element.TryGetProperty("content", out JsonElement contentElement)
                && contentElement.ValueKind == JsonValueKind.String)
            {
                return contentElement.GetString() ?? string.Empty;
            }

            if (element.TryGetProperty("payload", out JsonElement payloadElement))
            {
                string nested = ExtractRememberPrompt(payloadElement);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                string nested = ExtractRememberPrompt(child);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return string.Empty;
    }

    private static ConversationTrigger? ParseTrigger(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return null;
        }

        return Enum.TryParse<ConversationTrigger>(trigger, true, out var parsed) ? parsed : null;
    }

    private static ConversationStatus ParseConversationStatus(string status, ConversationStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return fallback;
        }

        string normalized = status.Trim();

        if (normalized.Equals("STARTED", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationStatus.Running;
        }

        return Enum.TryParse<ConversationStatus>(normalized, true, out var parsed) ? parsed : fallback;
    }

    private static void PopulateInitialEvents(
        ConversationMetadataRecord metadata,
        IReadOnlyList<RuntimeConversationEventDto> events,
        CreateConversationRequestDto request,
        DateTimeOffset now)
    {
        metadata.Events.Clear();
        if (events is not null && events.Count > 0)
        {
            foreach (RuntimeConversationEventDto evt in events.OrderBy(evt => evt.EventId))
            {
                if (evt.EventId <= 0)
                {
                    continue;
                }

                metadata.Events.Add(new ConversationEventRecord
                {
                    EventId = evt.EventId,
                    CreatedAtUtc = evt.CreatedAt == default ? now : evt.CreatedAt,
                    Type = string.IsNullOrWhiteSpace(evt.Type) ? "event" : evt.Type!,
                    PayloadJson = string.IsNullOrWhiteSpace(evt.PayloadJson) ? "{}" : evt.PayloadJson!,
                    ConversationMetadataRecordId = metadata.Id
                });
            }

            DateTimeOffset latest = metadata.Events.Count > 0
                ? metadata.Events.Max(evt => evt.CreatedAtUtc)
                : now;
            metadata.LastUpdatedAtUtc = latest.UtcDateTime;
            return;
        }

        int eventId = 1;

        if (!string.IsNullOrWhiteSpace(request.InitialUserMessage))
        {
            metadata.Events.Add(new ConversationEventRecord
            {
                EventId = eventId++,
                CreatedAtUtc = now,
                Type = "message",
                PayloadJson = OpenHandsEventPayloadBuilder.CreateUserMessageEvent(eventId - 1, now, request.InitialUserMessage!),
                ConversationMetadataRecordId = metadata.Id
            });
        }

        if (!string.IsNullOrWhiteSpace(request.ConversationInstructions))
        {
            metadata.Events.Add(new ConversationEventRecord
            {
                EventId = eventId++,
                CreatedAtUtc = now,
                Type = "recall",
                PayloadJson = OpenHandsEventPayloadBuilder.CreateConversationInstructionsEvent(
                    eventId - 1,
                    now,
                    request.ConversationInstructions!),
                ConversationMetadataRecordId = metadata.Id
            });
        }

        metadata.LastUpdatedAtUtc = metadata.Events.Count > 0
            ? metadata.Events.Max(evt => evt.CreatedAtUtc).UtcDateTime
            : now.UtcDateTime;
    }

    private static ConversationRuntimeInstanceRecord EnsureRuntimeInstance(ConversationMetadataRecord conversation)
    {
        if (conversation.RuntimeInstance is null)
        {
            conversation.RuntimeInstance = new ConversationRuntimeInstanceRecord
            {
                Id = Guid.NewGuid(),
                ConversationMetadataRecordId = conversation.Id,
                Conversation = conversation,
                RuntimeId = conversation.RuntimeId ?? string.Empty,
                SessionId = conversation.SessionId ?? string.Empty,
                SessionApiKey = conversation.SessionApiKey,
                RuntimeStatus = conversation.RuntimeStatus,
                Status = NormalizeStatus(conversation.Status),
                VscodeUrl = conversation.VscodeUrl,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        return conversation.RuntimeInstance;
    }

    private async Task<ConversationEventsPageDto> GetPersistedEventsAsync(
        ConversationMetadataRecord conversation,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        bool excludeHidden,
        CancellationToken cancellationToken)
    {
        int fetchLimit = Math.Min(limit + 1, MaxPageSize + 1);

        IReadOnlyList<ConversationEventRecord> records = await _repository
            .GetEventsAsync(conversation.Id, startId, endId, reverse, fetchLimit, cancellationToken)
            .ConfigureAwait(false);

        var events = new List<ConversationEventDto>(Math.Min(limit, records.Count));
        int processed = 0;

        foreach (ConversationEventRecord record in records)
        {
            processed++;

            if (!TryParseEventPayload(record.PayloadJson, out JsonElement payload))
            {
                _logger.LogWarning(
                    "Skipping persisted event {EventId} for conversation {ConversationId} because payload could not be parsed.",
                    record.EventId,
                    conversation.ConversationId);
                continue;
            }

            if (excludeHidden && IsHidden(payload))
            {
                continue;
            }

            events.Add(ToEventDto(record, payload));

            if (events.Count >= limit)
            {
                break;
            }
        }

        bool hasMore = processed < records.Count || records.Count == fetchLimit;

        return new ConversationEventsPageDto
        {
            Events = events,
            HasMore = hasMore
        };
    }

    private static ConversationEventDto ToEventDto(ConversationEventRecord record, JsonElement payload)
    {
        return new ConversationEventDto
        {
            Id = record.EventId,
            Type = string.IsNullOrWhiteSpace(record.Type) ? "event" : record.Type,
            CreatedAt = record.CreatedAtUtc,
            Event = payload
        };
    }

    private static bool TryParseEventPayload(string payloadJson, out JsonElement payload)
    {
        payload = default;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payloadJson);
            payload = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsHidden(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (element.TryGetProperty("hidden", out JsonElement hiddenElement)
            && hiddenElement.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (element.TryGetProperty("extras", out JsonElement extras)
            && extras.ValueKind == JsonValueKind.Object
            && extras.TryGetProperty("hidden", out JsonElement extrasHidden)
            && extrasHidden.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        return false;
    }

    private static string EnsureConversationUrl(ConversationMetadataRecord conversation, string host)
    {
        var fullUrl = HostExtensions.BuildFullUrl(host, conversation.Url);

        if (!string.IsNullOrWhiteSpace(fullUrl))
        {
            return fullUrl!;
        }

        if (!string.IsNullOrWhiteSpace(conversation.ConversationId))
        {
            return $"/api/conversations/{conversation.ConversationId}";
        }

        throw new ConversationRuntimeUnavailableException(
            conversation.ConversationId ?? string.Empty,
            "conversation-url");
    }

    private void LogConversationStatusUpdate(
        ConversationMetadataRecord conversation,
        ConversationStatus previousStatus,
        string previousRuntimeStatus,
        RuntimeConversationOperationResult result,
        string operation)
    {
        string previousStatusNormalized = NormalizeStatus(previousStatus);
        string currentStatusNormalized = NormalizeStatus(conversation.Status);
        string previousRuntimeRaw = previousRuntimeStatus ?? string.Empty;
        string currentRuntimeRaw = conversation.RuntimeStatus ?? string.Empty;
        string previousRuntimeLog = string.IsNullOrWhiteSpace(previousRuntimeStatus) ? "<null>" : previousRuntimeStatus!;
        string currentRuntimeLog = string.IsNullOrWhiteSpace(conversation.RuntimeStatus) ? "<null>" : conversation.RuntimeStatus!;

        bool statusChanged = !string.Equals(previousStatusNormalized, currentStatusNormalized, StringComparison.OrdinalIgnoreCase);
        bool runtimeChanged = !string.Equals(previousRuntimeRaw, currentRuntimeRaw, StringComparison.OrdinalIgnoreCase);

        string gatewayMessage = string.IsNullOrWhiteSpace(result.Message) ? "<none>" : result.Message!;
        string gatewayRuntimeId = string.IsNullOrWhiteSpace(result.RuntimeId) ? "<null>" : result.RuntimeId!;
        string gatewaySessionId = string.IsNullOrWhiteSpace(result.SessionId) ? "<null>" : result.SessionId!;
        string gatewayRuntimeUrl = string.IsNullOrWhiteSpace(result.RuntimeUrl) ? "<null>" : result.RuntimeUrl!;
        string gatewayVscodeUrl = string.IsNullOrWhiteSpace(result.VscodeUrl) ? "<null>" : result.VscodeUrl!;
        string providerSummary = result.Providers is { Count: > 0 }
            ? string.Join(',', result.Providers)
            : "<none>";
        int hostCount = result.Hosts?.Count ?? 0;

        if (statusChanged || runtimeChanged)
        {
            _logger.LogInformation(
                "Conversation {ConversationId} {Operation} operation updated status. PreviousStatus={PreviousStatus}; NewStatus={NewStatus}; PreviousRuntimeStatus={PreviousRuntimeStatus}; NewRuntimeStatus={NewRuntimeStatus}; GatewayStatus={GatewayStatus}; GatewayConversationStatus={GatewayConversationStatus}; GatewayRuntimeStatus={GatewayRuntimeStatus}; GatewayMessage={GatewayMessage}; GatewayRuntimeId={GatewayRuntimeId}; GatewaySessionId={GatewaySessionId}; GatewayRuntimeUrl={GatewayRuntimeUrl}; GatewayVscodeUrl={GatewayVscodeUrl}; GatewayProviders={GatewayProviders}; GatewayHostCount={GatewayHostCount}; Placeholder={IsPlaceholder}",
                conversation.ConversationId,
                operation,
                previousStatusNormalized,
                currentStatusNormalized,
                previousRuntimeLog,
                currentRuntimeLog,
                result.Status,
                result.ConversationStatus ?? "<null>",
                result.RuntimeStatus ?? "<null>",
                gatewayMessage,
                gatewayRuntimeId,
                gatewaySessionId,
                gatewayRuntimeUrl,
                gatewayVscodeUrl,
                providerSummary,
                hostCount,
                result.IsPlaceholder);
        }
        else
        {
            _logger.LogWarning(
                "Conversation {ConversationId} {Operation} operation did not change status. Status={Status}; RuntimeStatus={RuntimeStatus}; GatewayStatus={GatewayStatus}; GatewayConversationStatus={GatewayConversationStatus}; GatewayRuntimeStatus={GatewayRuntimeStatus}; GatewayMessage={GatewayMessage}; GatewayRuntimeId={GatewayRuntimeId}; GatewaySessionId={GatewaySessionId}; GatewayRuntimeUrl={GatewayRuntimeUrl}; GatewayVscodeUrl={GatewayVscodeUrl}; GatewayProviders={GatewayProviders}; GatewayHostCount={GatewayHostCount}; Placeholder={IsPlaceholder}",
                conversation.ConversationId,
                operation,
                currentStatusNormalized,
                currentRuntimeLog,
                result.Status,
                result.ConversationStatus ?? "<null>",
                result.RuntimeStatus ?? "<null>",
                gatewayMessage,
                gatewayRuntimeId,
                gatewaySessionId,
                gatewayRuntimeUrl,
                gatewayVscodeUrl,
                providerSummary,
                hostCount,
                result.IsPlaceholder);
        }
    }

    private void UpdateConversationFromOperation(
        ConversationMetadataRecord conversation,
        RuntimeConversationOperationResult result,
        IEnumerable<string> providers)
    {
        bool isPlaceholder = result.IsPlaceholder;
        ConversationStatus updatedStatus = ParseConversationStatus(result.ConversationStatus, conversation.Status);

        if (!isPlaceholder)
        {
            conversation.Status = updatedStatus;
        }
        else if (updatedStatus == ConversationStatus.Stopped)
        {
            conversation.Status = updatedStatus;
        }
        else
        {
            conversation.Status = ConversationStatus.Error;
        }

        if (!string.IsNullOrWhiteSpace(result.RuntimeStatus))
        {
            conversation.RuntimeStatus = result.RuntimeStatus;
        }

        if (!isPlaceholder)
        {
            if (!string.IsNullOrWhiteSpace(result.SessionApiKey))
            {
                conversation.SessionApiKey = result.SessionApiKey;
            }

            if (!string.IsNullOrWhiteSpace(result.SessionId))
            {
                conversation.SessionId = result.SessionId;
            }

            if (!string.IsNullOrWhiteSpace(result.RuntimeId))
            {
                conversation.RuntimeId = result.RuntimeId;
            }

            if (!string.IsNullOrWhiteSpace(result.RuntimeUrl))
            {
                conversation.Url = BuildConversationUrl(result.RuntimeUrl, null, conversation.ConversationId);
            }

            if (!string.IsNullOrWhiteSpace(result.VscodeUrl))
            {
                conversation.VscodeUrl = result.VscodeUrl;
            }
        }

        conversation.LastUpdatedAtUtc = DateTime.UtcNow;
        conversation.ConversationVersion = DetermineConversationVersion(conversation);

        conversation.Status = DeriveConversationStatus(conversation);

        ConversationRuntimeInstanceRecord runtime = EnsureRuntimeInstance(conversation);
        runtime.Status = NormalizeStatus(conversation.Status);
        runtime.RuntimeStatus = conversation.RuntimeStatus;

        if (!isPlaceholder)
        {
            runtime.RuntimeId = conversation.RuntimeId ?? runtime.RuntimeId;
            runtime.SessionId = conversation.SessionId ?? runtime.SessionId;
            runtime.SessionApiKey = conversation.SessionApiKey;
            runtime.VscodeUrl = conversation.VscodeUrl;

            IReadOnlyList<string> providerList = result.Providers is { Count: > 0 }
                ? result.Providers
                : providers?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).ToList();

            ApplyRuntimeProviders(runtime, providerList);

            if (result.Hosts is { Count: > 0 })
            {
                ApplyRuntimeHosts(runtime, result.Hosts);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(runtime.RuntimeId) && !string.IsNullOrWhiteSpace(result.RuntimeId))
            {
                runtime.RuntimeId = result.RuntimeId!;
            }

            if (string.IsNullOrWhiteSpace(runtime.SessionId) && !string.IsNullOrWhiteSpace(result.SessionId))
            {
                runtime.SessionId = result.SessionId!;
            }

            if (string.IsNullOrWhiteSpace(runtime.SessionApiKey))
            {
                runtime.SessionApiKey = conversation.SessionApiKey;
            }

            if (string.IsNullOrWhiteSpace(runtime.VscodeUrl) && !string.IsNullOrWhiteSpace(result.VscodeUrl))
            {
                runtime.VscodeUrl = result.VscodeUrl;
            }
        }
    }

    private static ConversationMicroagentRecord EnsureMicroagentDefaults(ConversationMicroagentRecord record)
    {
        record.TriggersJson ??= SerializeArray(Array.Empty<string>());
        record.InputsJson ??= SerializeArray(Array.Empty<InputMetadataDto>());
        record.ToolsJson ??= SerializeArray(Array.Empty<string>());
        return record;
    }

    private static MicroagentDto ToMicroagentDto(ConversationMicroagentRecord record)
    {
        record = EnsureMicroagentDefaults(record);

        IReadOnlyList<string> triggers = DeserializeArray<string>(record.TriggersJson);
        IReadOnlyList<InputMetadataDto> inputs = DeserializeArray<InputMetadataDto>(record.InputsJson);
        IReadOnlyList<string> tools = DeserializeArray<string>(record.ToolsJson);

        return new MicroagentDto
        {
            Name = record.Name,
            Type = record.Type,
            Content = record.Content,
            Triggers = triggers,
            Inputs = inputs,
            Tools = tools
        };
    }

    private static string DetermineConversationVersion(CreateConversationRequestDto request)
    {
        Console.WriteLine("DetermineConversationVersion request:{0},{1}", request?.SandboxConnection, request?.SandboxId);

        // Default to V1 so the UI uses the new WebSocket event pipeline even when
        // explicit sandbox metadata is not supplied. The V0 Socket.IO path is no
        // longer wired up in this application, which caused conversations to start
        // but never stream messages back to the frontend.
        if (request is null)
        {
            return "V1";
        }

        if (request.SandboxConnection is not null || !string.IsNullOrWhiteSpace(request.SandboxId))
        {
            return "V1";
        }

        return "V1";
    }

    private static string DetermineConversationVersion(ConversationMetadataRecord record)
    {
        Console.WriteLine("DetermineConversationVersion record:{0},{1}", record.ConversationVersion, record.SandboxId);
        if (!string.IsNullOrWhiteSpace(record.ConversationVersion))
        {
            return record.ConversationVersion!;
        }

        if (!string.IsNullOrWhiteSpace(record.SandboxId))
        {
            return "V1";
        }

        // When no explicit version is stored, assume V1 to align with the
        // runtime WebSocket implementation. Falling back to V0 prevented
        // messages from streaming to the UI.
        return "V1";
    }

    private static string DetermineTitle(CreateConversationRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            return request.Title!;
        }

        if (!string.IsNullOrWhiteSpace(request.SuggestedTask?.Title))
        {
            return request.SuggestedTask!.Title!;
        }

        if (!string.IsNullOrWhiteSpace(request.Repository))
        {
            return request.Repository!;
        }

        return "New Conversation";
    }

    private static ConversationTrigger DetermineTrigger(CreateConversationRequestDto request)
    {
        if (request.CreateMicroagent is not null)
        {
            return ConversationTrigger.MicroagentManagement;
        }

        if (request.SuggestedTask is not null)
        {
            return ConversationTrigger.SuggestedTask;
        }

        return ConversationTrigger.Gui;
    }

    private static ProviderType? TryParseProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return Enum.TryParse<ProviderType>(provider, true, out var parsed) ? parsed : null;
    }

    private static ConversationStatus DeriveConversationStatus(ConversationMetadataRecord record)
    {
        ConversationStatus status = record.Status;

        ConversationStatus? mappedFromRuntime = MapRuntimeStatus(record.RuntimeStatus);
        if (!mappedFromRuntime.HasValue && record.RuntimeInstance is not null)
        {
            mappedFromRuntime = MapRuntimeStatus(record.RuntimeInstance.RuntimeStatus)
                ?? MapConversationStatus(record.RuntimeInstance.Status);
        }

        if (mappedFromRuntime.HasValue)
        {
            status = mappedFromRuntime.Value;
        }

        return status;
    }

    private static ConversationStatus? MapConversationStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<ConversationStatus>(status, true, out var parsed) ? parsed : null;
    }

    private string BuildConversationUrl(
        string runtimeUrl,
        SandboxConnectionInfoDto sandboxConnection,
        string conversationId)
    {
        string baseUrl = runtimeUrl;
        baseUrl ??= sandboxConnection?.AgentServerUrl;
        baseUrl ??= sandboxConnection?.RuntimeUrl;

        // Fallback to the configured runtime gateway host when no runtime URL was provided
        // so the frontend connects directly to the runtime service instead of the API host.
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = _runtimeGatewayBaseUri?.ToString();
        }

        // Absolute fallback (relative path) when we can't determine any host information
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"/api/conversations/{conversationId}";
        }

        string normalizedBase = baseUrl.Trim();

        // If the runtime already returned a conversation-specific URL, respect it
        if (!normalizedBase.Contains(conversationId, StringComparison.Ordinal))
        {
            normalizedBase = normalizedBase.TrimEnd('/');
            normalizedBase = $"{normalizedBase}/api/conversations/{conversationId}";
        }

        if (!Uri.TryCreate(normalizedBase, UriKind.Absolute, out Uri absoluteUri))
        {
            // Treat the runtime URL as relative to the runtime gateway base when available
            if (_runtimeGatewayBaseUri is not null)
            {
                string relativePath = normalizedBase.StartsWith("/", StringComparison.Ordinal)
                    ? normalizedBase
                    : $"/{normalizedBase}";

                absoluteUri = new Uri(_runtimeGatewayBaseUri, relativePath);
            }
            else
            {
                // As a last resort, return a normalized relative path
                return normalizedBase.StartsWith("/", StringComparison.Ordinal)
                    ? normalizedBase
                    : $"/{normalizedBase}";
            }
        }

        return absoluteUri.ToString();

        //int conversationsIndex = path.IndexOf("/api/conversations", StringComparison.OrdinalIgnoreCase);
        //if (conversationsIndex >= 0)
        //{
        //    path = path[..conversationsIndex].TrimEnd('/');
        //}

        //if (!string.IsNullOrEmpty(path))
        //{
        //    path = $"{path.TrimEnd('/')}/api/conversations/{conversationId}";
        //}
        //else
        //{
        //    path = $"/api/conversations/{conversationId}";
        //}

        //builder.Path = path;
        //builder.Query = string.Empty;
        //builder.Fragment = string.Empty;

        //return builder.Uri.ToString().TrimEnd('/');
    }

    private static Uri CreateRuntimeGatewayBaseUri(RuntimeConversationGatewayOptions options)
    {
        string baseUrl = options?.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = Environment.GetEnvironmentVariable("RUNTIME_GATEWAY_BASE_URL")
                ?? Environment.GetEnvironmentVariable("NETAI_RUNTIME_BASE_URL");
        }

        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri)
                ? uri
                : null;
    }

    private static bool IsSimpleHostname(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        string normalized = host.Trim();
        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(normalized, out _))
        {
            return false;
        }

        return normalized.IndexOf('.', StringComparison.Ordinal) < 0;
    }

    private static ConversationStatus? MapRuntimeStatus(string runtimeStatus)
    {
        if (string.IsNullOrWhiteSpace(runtimeStatus))
        {
            return null;
        }

        string normalized = runtimeStatus.Trim();

        if (ContainsToken(normalized, RuntimeReadyTokens))
        {
            return ConversationStatus.Running;
        }

        if (ContainsToken(normalized, RuntimeStoppedTokens))
        {
            return ConversationStatus.Stopped;
        }

        if (normalized.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationStatus.Error;
        }

        return null;
    }

    private static bool ContainsToken(string value, IReadOnlyCollection<string> tokens)
    {
        foreach (string token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            int index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                int startIndex = index;
                int endIndex = index + token.Length;

                bool startBoundary = startIndex == 0 || IsBoundary(value[startIndex - 1]);
                bool endBoundary = endIndex >= value.Length || IsBoundary(value[endIndex]);

                if (startBoundary && endBoundary)
                {
                    return true;
                }

                index = value.IndexOf(token, index + token.Length, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static bool IsBoundary(char c)
    {
        return !char.IsLetterOrDigit(c) && c != '_';
    }

    private static string NormalizeStatus(ConversationStatus status)
    {
        return status.ToString().ToUpperInvariant();
    }

    private static string SerializeArray<T>(IEnumerable<T> values)
    {
        return JsonSerializer.Serialize(values);
    }

    private static IReadOnlyList<T> DeserializeArray<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<T>();
        }

        try
        {
            T[] values = JsonSerializer.Deserialize<T[]>(json);
            return values ?? Array.Empty<T>();
        }
        catch
        {
            return Array.Empty<T>();
        }
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime? value)
    {
        return value.HasValue ? ToDateTimeOffset(value.Value) : DateTimeOffset.UtcNow;
    }
}
