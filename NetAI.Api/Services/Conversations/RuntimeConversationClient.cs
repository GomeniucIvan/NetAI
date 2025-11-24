using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using NetAI.Api.Data.Entities.Conversations;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;

public sealed class RuntimeConversationClient : IRuntimeConversationClient
{
    private const int MaxEventFetchSize = 1000;

    private static readonly HashSet<string> TrajectoryExcludedFields = new(
        new[]
        {
            "dom_object",
            "axtree_object",
            "active_page_index",
            "last_browser_action",
            "last_browser_action_error",
            "focused_element_bid",
            "extra_element_properties",
            "screenshot",
            "set_of_marks"
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly IConversationRepository _repository;
    private readonly IRuntimeConversationGateway _runtimeGateway;
    private readonly ILogger<RuntimeConversationClient> _logger;

    public RuntimeConversationClient(
        IConversationRepository repository,
        IRuntimeConversationGateway runtimeGateway,
        ILogger<RuntimeConversationClient> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _runtimeGateway = runtimeGateway ?? throw new ArgumentNullException(nameof(runtimeGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RuntimeConversationHandle> AttachAsync(
        ConversationMetadataRecord conversation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RuntimeConversationHandle(conversation));
    }

    public Task DetachAsync(RuntimeConversationHandle handle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task StartAsync(
        RuntimeConversationHandle handle,
        IEnumerable<string> providers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = handle.Conversation;
        DateTime utcNow = DateTime.UtcNow;
        conversation.Status = ConversationStatus.Running;
        conversation.RuntimeStatus = "STATUS$READY";
        conversation.LastUpdatedAtUtc = utcNow;

        ConversationRuntimeInstanceRecord runtime = EnsureRuntime(conversation);
        runtime.Status = NormalizeStatus(conversation.Status);
        runtime.RuntimeStatus = conversation.RuntimeStatus;
        runtime.SessionApiKey = conversation.SessionApiKey;
        runtime.VscodeUrl = conversation.VscodeUrl;

        if (providers is not null)
        {
            runtime.Providers.Clear();
            foreach (string provider in providers)
            {
                if (string.IsNullOrWhiteSpace(provider))
                {
                    continue;
                }

                runtime.Providers.Add(new ConversationRuntimeProviderRecord
                {
                    Id = Guid.NewGuid(),
                    ConversationRuntimeInstanceRecordId = runtime.Id,
                    Provider = provider
                });
            }
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(RuntimeConversationHandle handle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = handle.Conversation;
        DateTime utcNow = DateTime.UtcNow;
        conversation.Status = ConversationStatus.Stopped;
        conversation.RuntimeStatus = "STATUS$STOPPED";
        conversation.LastUpdatedAtUtc = utcNow;

        ConversationRuntimeInstanceRecord runtime = EnsureRuntime(conversation);
        runtime.Status = NormalizeStatus(conversation.Status);
        runtime.RuntimeStatus = conversation.RuntimeStatus;
        runtime.SessionApiKey = conversation.SessionApiKey;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SendMessageAsync(
        RuntimeConversationHandle handle,
        string message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        ConversationMetadataRecord conversation = handle.Conversation;
        string conversationUrl = EnsureConversationUrl(conversation);

        try
        {
            await _runtimeGateway
                .PostMessageAsync(
                    new RuntimeConversationMessageRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = conversation.SessionApiKey,
                        Message = message,
                        Source = "user"
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RuntimeConversationGatewayException ex)
        {
            throw TranslateGatewayException(conversation.ConversationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }

        conversation.LastUpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task SendEventAsync(
        RuntimeConversationHandle handle,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = handle.Conversation;
        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Event payload is required.", nameof(payload));
        }

        string conversationUrl = EnsureConversationUrl(conversation);

        try
        {
            await _runtimeGateway
                .PostEventAsync(
                    new RuntimeConversationEventRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = conversation.SessionApiKey,
                        Payload = payload
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RuntimeConversationGatewayException ex)
        {
            throw TranslateGatewayException(conversation.ConversationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }

        conversation.LastUpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConversationEventsPageDto> GetEventsAsync(
        RuntimeConversationHandle handle,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        bool excludeHidden,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be at least 1.");
        }

        (List<JsonElement> events, bool hasMore) = await FetchVisibleEventsAsync(
            handle,
            startId,
            endId,
            reverse,
            limit,
            excludeHidden,
            cancellationToken).ConfigureAwait(false);

        List<ConversationEventDto> dtos = events
            .Select(ToEventDto)
            .ToList();

        return new ConversationEventsPageDto
        {
            Events = dtos,
            HasMore = hasMore
        };
    }

    public async Task<TrajectoryResponseDto> GetTrajectoryAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationMetadataRecord conversation = handle.Conversation;
        List<JsonElement> events = await FetchAllEventsAsync(
            conversation,
            excludeHidden: true,
            cancellationToken).ConfigureAwait(false);

        var trajectory = new List<JsonElement>(events.Count);
        foreach (JsonElement element in events
            .OrderBy(evt => TryGetEventId(evt, out int id) ? id : int.MaxValue))
        {
            trajectory.Add(ToTrajectoryElement(element));
        }

        return new TrajectoryResponseDto
        {
            Trajectory = trajectory,
            Error = null
        };
    }

    public Task<RuntimeConfigResponseDto> GetRuntimeConfigAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationRuntimeInstanceRecord runtime = EnsureRuntime(handle.Conversation);

        if (string.IsNullOrWhiteSpace(runtime.RuntimeId) && string.IsNullOrWhiteSpace(runtime.SessionId))
        {
            throw new ConversationRuntimeUnavailableException(
                handle.Conversation.ConversationId,
                "runtime-config");
        }

        return Task.FromResult(new RuntimeConfigResponseDto
        {
            RuntimeId = runtime.RuntimeId,
            SessionId = runtime.SessionId
        });
    }

    public Task<VSCodeUrlResponseDto> GetVSCodeUrlAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(handle.Conversation.VscodeUrl))
        {
            throw new ConversationRuntimeUnavailableException(
                handle.Conversation.ConversationId,
                "vscode-url");
        }

        return Task.FromResult(new VSCodeUrlResponseDto
        {
            VscodeUrl = handle.Conversation.VscodeUrl,
            Error = null
        });
    }

    public Task<WebHostsResponseDto> GetWebHostsAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationRuntimeInstanceRecord runtime = EnsureRuntime(handle.Conversation);

        if (runtime.Hosts.Count == 0)
        {
            throw new ConversationRuntimeUnavailableException(
                handle.Conversation.ConversationId,
                "web-hosts");
        }

        Dictionary<string, string> hosts = runtime.Hosts
            .ToDictionary(host => host.Name, host => host.Url, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(new WebHostsResponseDto
        {
            Hosts = hosts
        });
    }

    public Task<string> GetSecurityAnalyzerUrlAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConversationRuntimeInstanceRecord runtime = EnsureRuntime(handle.Conversation);

        ConversationRuntimeHostRecord analyzerHost = runtime.Hosts
            .FirstOrDefault(host => string.Equals(host.Name, "security", StringComparison.OrdinalIgnoreCase));

        if (analyzerHost is null || string.IsNullOrWhiteSpace(analyzerHost.Url))
        {
            throw new ConversationResourceNotFoundException(handle.Conversation.ConversationId, "security-analyzer");
        }

        if (!Uri.TryCreate(analyzerHost.Url, UriKind.Absolute, out Uri analyzerUri))
        {
            throw new ConversationRuntimeActionException(
                handle.Conversation.ConversationId,
                "Invalid security analyzer endpoint");
        }

        return Task.FromResult(analyzerUri.ToString());
    }

    public async Task<IReadOnlyList<MicroagentDto>> GetMicroagentsAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string conversationUrl = EnsureConversationUrl(handle.Conversation);

        try
        {
            RuntimeConversationMicroagentsResult result = await _runtimeGateway
                .GetMicroagentsAsync(
                    new RuntimeConversationMicroagentsRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = handle.Conversation.SessionApiKey
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return result.Microagents ?? Array.Empty<MicroagentDto>();
        }
        catch (RuntimeConversationGatewayException ex)
        {
            throw TranslateGatewayException(handle.Conversation.ConversationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
    }

    private async Task<(List<JsonElement> Events, bool HasMore)> FetchVisibleEventsAsync(
        RuntimeConversationHandle handle,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        bool excludeHidden,
        CancellationToken cancellationToken)
    {
        ConversationMetadataRecord conversation = handle.Conversation;
        string conversationUrl = EnsureConversationUrl(conversation);

        int targetCount = limit + 1;
        var visible = new List<JsonElement>();
        int startCursor = Math.Max(startId, 0);
        int? paginationEndCursor = null;
        bool reachedBoundary = false;
        bool moreAvailable = false;
        bool reachedFetchLimit = false;
        int totalFetched = 0;

        while (visible.Count < targetCount && !reachedBoundary)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int requestLimit = Math.Min(100, Math.Max(targetCount - visible.Count, 1));
            RuntimeConversationEventsResult page = await FetchEventsPageAsync(
                conversation,
                conversationUrl,
                startCursor,
                reverse ? paginationEndCursor : endId,
                reverse,
                requestLimit,
                cancellationToken).ConfigureAwait(false);

            if (page.Events.Count == 0)
            {
                moreAvailable = page.HasMore;
                break;
            }

            totalFetched += page.Events.Count;
            if (totalFetched >= MaxEventFetchSize)
            {
                _logger.LogDebug(
                    "Reached maximum fetch size while retrieving events for conversation {ConversationId}.",
                    conversation.ConversationId);
                reachedFetchLimit = true;
            }

            List<int> pageIds = new();
            foreach (JsonElement evt in page.Events)
            {
                if (!TryGetEventId(evt, out int eventId))
                {
                    continue;
                }

                pageIds.Add(eventId);

                if (endId.HasValue)
                {
                    if (!reverse && eventId > endId.Value)
                    {
                        reachedBoundary = true;
                        moreAvailable = false;
                        break;
                    }

                    if (reverse && eventId < endId.Value)
                    {
                        reachedBoundary = true;
                        moreAvailable = false;
                        break;
                    }
                }

                if (excludeHidden && IsHidden(evt))
                {
                    continue;
                }

                visible.Add(evt.Clone());
                if (visible.Count >= targetCount)
                {
                    break;
                }
            }

            if (reverse)
            {
                if (pageIds.Count > 0)
                {
                    int minId = pageIds.Min();
                    paginationEndCursor = minId - 1;
                    if (paginationEndCursor < 0)
                    {
                        reachedBoundary = true;
                    }
                }
                else
                {
                    reachedBoundary = true;
                }
            }
            else
            {
                if (pageIds.Count > 0)
                {
                    int maxId = pageIds.Max();
                    startCursor = Math.Max(startCursor, maxId + 1);
                }
                else
                {
                    reachedBoundary = true;
                }
            }

            if (visible.Count >= targetCount)
            {
                break;
            }

            if (!page.HasMore)
            {
                moreAvailable = false;
                break;
            }

            moreAvailable = true;

            if (reachedFetchLimit)
            {
                break;
            }
        }

        bool hasMore = visible.Count > limit;
        if (!hasMore && (moreAvailable || reachedFetchLimit) && !reachedBoundary)
        {
            hasMore = true;
        }

        List<JsonElement> trimmed = visible.Count > limit ? visible.Take(limit).ToList() : visible;
        return (trimmed, hasMore);
    }

    private async Task<List<JsonElement>> FetchAllEventsAsync(
        ConversationMetadataRecord conversation,
        bool excludeHidden,
        CancellationToken cancellationToken)
    {
        string conversationUrl = EnsureConversationUrl(conversation);
        var results = new List<JsonElement>();
        int startCursor = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RuntimeConversationEventsResult page = await FetchEventsPageAsync(
                conversation,
                conversationUrl,
                startCursor,
                endId: null,
                reverse: false,
                limit: 100,
                cancellationToken).ConfigureAwait(false);

            if (page.Events.Count == 0)
            {
                break;
            }

            int? maxId = null;
            foreach (JsonElement evt in page.Events)
            {
                if (TryGetEventId(evt, out int eventId))
                {
                    maxId = maxId is null ? eventId : Math.Max(maxId.Value, eventId);
                }

                if (excludeHidden && IsHidden(evt))
                {
                    continue;
                }

                results.Add(evt.Clone());
            }

            if (!page.HasMore || !maxId.HasValue)
            {
                break;
            }

            startCursor = Math.Max(startCursor, maxId.Value + 1);
        }

        return results;
    }

    private async Task<RuntimeConversationEventsResult> FetchEventsPageAsync(
        ConversationMetadataRecord conversation,
        string conversationUrl,
        int startId,
        int? endId,
        bool reverse,
        int? limit,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _runtimeGateway
                .GetEventsAsync(
                    new RuntimeConversationEventsRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = conversation.SessionApiKey,
                        StartId = startId,
                        EndId = endId,
                        Reverse = reverse,
                        Limit = limit
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RuntimeConversationGatewayException ex)
        {
            throw TranslateGatewayException(conversation.ConversationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeUnavailableException(conversation.ConversationId, ex.Message);
        }
    }

    private static string EnsureConversationUrl(ConversationMetadataRecord conversation)
    {
        if (!string.IsNullOrWhiteSpace(conversation.Url))
        {
            return conversation.Url!;
        }

        if (!string.IsNullOrWhiteSpace(conversation.ConversationId))
        {
            return $"/api/conversations/{conversation.ConversationId}";
        }

        throw new ConversationRuntimeUnavailableException(
            conversation.ConversationId ?? string.Empty,
            "conversation-url");
    }

    private static ConversationSessionException TranslateGatewayException(
        string conversationId,
        RuntimeConversationGatewayException exception)
    {
        return exception.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => new ConversationUnauthorizedException(conversationId),
            HttpStatusCode.NotFound
                => new ConversationRuntimeUnavailableException(conversationId, ExtractGatewayError(exception)),
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => new ConversationRuntimeUnavailableException(conversationId, exception.Message),
            _ => new ConversationRuntimeActionException(conversationId, ExtractGatewayError(exception))
        };
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


    public async Task<IReadOnlyList<string>> ListFilesAsync(
        RuntimeConversationHandle handle,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string conversationUrl = EnsureConversationUrl(handle.Conversation);

        try
        {
            IReadOnlyList<string> files = await _runtimeGateway
                .ListFilesAsync(
                    new RuntimeConversationFileListRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = handle.Conversation.SessionApiKey,
                        Path = path
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return files;
        }
        catch (RuntimeConversationGatewayException ex)
        {
            throw TranslateGatewayException(handle.Conversation.ConversationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
    }

    public async Task<RuntimeObservation> RunActionAsync(
        RuntimeConversationHandle handle,
        IRuntimeAction action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (action is RuntimeFileReadAction fileRead)
        {
            string conversationUrl = EnsureConversationUrl(handle.Conversation);

            try
            {
                RuntimeConversationFileSelectionResult result = await _runtimeGateway
                    .SelectFileAsync(
                        new RuntimeConversationFileSelectionRequest
                        {
                            ConversationUrl = conversationUrl,
                            SessionApiKey = handle.Conversation.SessionApiKey,
                            File = fileRead.Path
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(result.Code))
                {
                    return new RuntimeFileReadObservation(result.Code!);
                }

                if (result.IsBinary)
                {
                    return new RuntimeErrorObservation("ERROR_BINARY_FILE");
                }

                string error = string.IsNullOrWhiteSpace(result.Error)
                    ? "Error opening file"
                    : result.Error!;

                return new RuntimeErrorObservation(error);
            }
            catch (RuntimeConversationGatewayException ex)
            {
                throw TranslateGatewayException(handle.Conversation.ConversationId, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
            }
        }

        return new RuntimeErrorObservation(
            $"Unsupported action type: {action.GetType().Name}");
    }

    public async Task<RuntimeZipStreamResult> ZipWorkspaceAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string conversationUrl = EnsureConversationUrl(handle.Conversation);

        try
        {
            RuntimeZipStreamResult result = await _runtimeGateway
                .ZipWorkspaceAsync(
                    new RuntimeConversationZipRequest
                    {
                        ConversationUrl = conversationUrl,
                        SessionApiKey = handle.Conversation.SessionApiKey
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (RuntimeConversationGatewayException ex)
        {
            throw TranslateGatewayException(handle.Conversation.ConversationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConversationRuntimeUnavailableException(handle.Conversation.ConversationId, ex.Message);
        }
    }

    private static ConversationRuntimeInstanceRecord EnsureRuntime(ConversationMetadataRecord conversation)
    {
        if (conversation.RuntimeInstance is not null)
        {
            return conversation.RuntimeInstance;
        }

        conversation.RuntimeInstance = new ConversationRuntimeInstanceRecord
        {
            Id = Guid.NewGuid(),
            ConversationMetadataRecordId = conversation.Id,
            RuntimeId = conversation.RuntimeId ?? string.Empty,
            SessionId = conversation.SessionId ?? string.Empty,
            SessionApiKey = conversation.SessionApiKey,
            RuntimeStatus = conversation.RuntimeStatus,
            Status = NormalizeStatus(conversation.Status),
            VscodeUrl = conversation.VscodeUrl,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        return conversation.RuntimeInstance;
    }

    private static ConversationEventDto ToEventDto(JsonElement element)
    {
        int id = TryGetEventId(element, out int eventId) ? eventId : 0;
        string type = DetermineEventType(element);
        DateTimeOffset timestamp = GetEventTimestamp(element);

        return new ConversationEventDto
        {
            Id = id,
            Type = type,
            CreatedAt = timestamp,
            Event = element.Clone()
        };
    }

    private static string NormalizeStatus(ConversationStatus status)
    {
        return status.ToString().ToUpperInvariant();
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

    private static bool TryGetEventId(JsonElement element, out int id)
    {
        id = 0;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("id", out JsonElement idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out int numeric))
        {
            id = numeric;
            return true;
        }

        if (idElement.ValueKind == JsonValueKind.String
            && int.TryParse(idElement.GetString(), out int parsed))
        {
            id = parsed;
            return true;
        }

        return false;
    }

    private static string DetermineEventType(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return "event";
        }

        if (element.TryGetProperty("action", out JsonElement actionElement)
            && actionElement.ValueKind == JsonValueKind.String)
        {
            return actionElement.GetString() ?? "event";
        }

        if (element.TryGetProperty("observation", out JsonElement observationElement)
            && observationElement.ValueKind == JsonValueKind.String)
        {
            return observationElement.GetString() ?? "event";
        }

        if (element.TryGetProperty("type", out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString() ?? "event";
        }

        return "event";
    }

    private static DateTimeOffset GetEventTimestamp(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("timestamp", out JsonElement timestampElement)
            && timestampElement.ValueKind == JsonValueKind.String)
        {
            string timestampValue = timestampElement.GetString();
            if (!string.IsNullOrWhiteSpace(timestampValue)
                && DateTimeOffset.TryParse(timestampValue, out DateTimeOffset timestamp))
            {
                return timestamp;
            }
        }

        return DateTimeOffset.MinValue;
    }

    private static JsonElement ToTrajectoryElement(JsonElement element)
    {
        JsonNode node = JsonNode.Parse(element.GetRawText());
        if (node is JsonObject obj && obj.TryGetPropertyValue("extras", out JsonNode extras) && extras is not null)
        {
            RemoveFields(extras, TrajectoryExcludedFields);
        }

        return node is null
            ? element
            : JsonSerializer.SerializeToElement(node);
    }

    private static void RemoveFields(JsonNode node, ISet<string> fields)
    {
        if (node is null)
        {
            return;
        }

        switch (node)
        {
            case JsonObject obj:
                foreach (string field in fields)
                {
                    obj.Remove(field);
                }

                foreach ((string _, JsonNode child) in obj)
                {
                    RemoveFields(child, fields);
                }

                break;

            case JsonArray array:
                foreach (JsonNode child in array)
                {
                    RemoveFields(child, fields);
                }

                break;
        }
    }
}
