using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services
{
    public class InMemoryConversationRuntime : IConversationRuntime
    {
        private readonly ConcurrentDictionary<string, ConversationState> _conversations = new(StringComparer.Ordinal);
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<InMemoryConversationRuntime> _logger;
        private readonly IWorkspaceService _workspace;
        private readonly IGitClient _gitClient;
        private readonly IFileEditService _fileEditor;

        public InMemoryConversationRuntime(
            TimeProvider timeProvider,
            ILogger<InMemoryConversationRuntime> logger,
            IWorkspaceService workspace,
            IGitClient gitClient,
            IFileEditService fileEditor)
        {
            _timeProvider = timeProvider;
            _logger = logger;
            _workspace = workspace;
            _gitClient = gitClient;
            _fileEditor = fileEditor;
        }

        public Task<RuntimeConversationInitResult> InitializeAsync(CreateConversationRequestDto request)
        {
            string id = Guid.NewGuid().ToString("N");
            DateTimeOffset timestamp = _timeProvider.GetUtcNow();
            string workspaceRoot = _workspace.EnsureProjectWorkspace(id);
            _logger.LogInformation("[RuntimeRuntime] Initialize conversation {ConversationId} at workspace {Workspace}", id, workspaceRoot);
            Console.WriteLine($"[RuntimeServer] InitializeAsync created workspace {workspaceRoot} for {id}");

            var state = new ConversationState(id, timestamp, workspaceRoot)
            {
                ConversationStatus = "CREATED",
                RuntimeStatus = "READY",
                SessionApiKey = GenerateSessionApiKey(),
                RuntimeId = $"runtime-{Guid.NewGuid():N}",
                SessionId = $"session-{Guid.NewGuid():N}",
                Url = $"/api/conversations/{id}",
                VscodeUrl = $"vscode://openhands/conversations/{id}"
            };

            PopulateHostMetadata(state);
            PopulateMicroagents(state, request);

            _conversations[id] = state;

            _logger.LogInformation("Conversation {ConversationId} created", id);

            return Task.FromResult(new RuntimeConversationInitResult
            {
                ConversationId = id,
                ConversationStatus = state.ConversationStatus,
                RuntimeStatus = state.RuntimeStatus,
                Message = "Conversation created successfully",
                SessionApiKey = state.SessionApiKey,
                RuntimeId = state.RuntimeId,
                SessionId = state.SessionId
            });
        }

        public Task<RuntimeConversationOperationResult?> StartAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                _logger.LogWarning("[RuntimeRuntime] StartAsync: conversation {ConversationId} not found", id);
                return Task.FromResult<RuntimeConversationOperationResult?>(null);
            }

            lock (state.SyncRoot)
            {
                state.ConversationStatus = "STARTED";
                state.RuntimeStatus = "RUNNING";
                state.StatusMessage = "Conversation started successfully";
                state.Touch(_timeProvider.GetUtcNow());
            }

            _logger.LogInformation("[RuntimeRuntime] StartAsync: {ConversationId} -> {Status}/{RuntimeStatus}", id, state.ConversationStatus, state.RuntimeStatus);
            Console.WriteLine($"[RuntimeServer] StartAsync completed for {id} with {state.ConversationStatus}/{state.RuntimeStatus}");

            return Task.FromResult<RuntimeConversationOperationResult?>(new RuntimeConversationOperationResult
            {
                ConversationId = id,
                ConversationStatus = state.ConversationStatus,
                RuntimeStatus = state.RuntimeStatus,
                Message = state.StatusMessage,
                SessionApiKey = state.SessionApiKey,
                RuntimeId = state.RuntimeId,
                SessionId = state.SessionId
            });
        }

        public Task<RuntimeConversationOperationResult?> StopAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                _logger.LogWarning("[RuntimeRuntime] StopAsync: conversation {ConversationId} not found", id);
                return Task.FromResult<RuntimeConversationOperationResult?>(null);
            }

            lock (state.SyncRoot)
            {
                state.ConversationStatus = "STOPPED";
                state.RuntimeStatus = "IDLE";
                state.StatusMessage = "Conversation stopped successfully";
                state.Touch(_timeProvider.GetUtcNow());
            }

            _logger.LogInformation("[RuntimeRuntime] StopAsync: {ConversationId} -> {Status}/{RuntimeStatus}", id, state.ConversationStatus, state.RuntimeStatus);
            Console.WriteLine($"[RuntimeServer] StopAsync completed for {id} with {state.ConversationStatus}/{state.RuntimeStatus}");

            return Task.FromResult<RuntimeConversationOperationResult?>(new RuntimeConversationOperationResult
            {
                ConversationId = id,
                ConversationStatus = state.ConversationStatus,
                RuntimeStatus = state.RuntimeStatus,
                Message = state.StatusMessage,
                SessionApiKey = state.SessionApiKey,
                RuntimeId = state.RuntimeId,
                SessionId = state.SessionId
            });
        }

        public Task<RuntimeConversationEventDto?> AppendMessageAsync(string id, string message, string? source = null)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationEventDto?>(null);
            }

            string normalizedSource = string.IsNullOrWhiteSpace(source)
                ? "user"
                : source!;

            var additional = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = JsonSerializer.SerializeToElement(normalizedSource),
                ["message"] = JsonSerializer.SerializeToElement(message)
            };

            RuntimeConversationEventDto evt;
            lock (state.SyncRoot)
            {
                evt = state.AddEvent(
                    _timeProvider.GetUtcNow(),
                    "message",
                    additional);
            }

            _logger.LogInformation("Message appended to conversation {ConversationId}: {Message}", id, message);
            Console.WriteLine("Message appended to conversation {0}: {1}", id, message);

            return Task.FromResult<RuntimeConversationEventDto>(evt);
        }

        public Task<RuntimeConversationStateDto?> GetConversationAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationStateDto?>(null);
            }

            lock (state.SyncRoot)
            {
                return Task.FromResult<RuntimeConversationStateDto?>(new RuntimeConversationStateDto
                {
                    ConversationId = state.Id,
                    Status = state.ConversationStatus,
                    RuntimeStatus = state.RuntimeStatus,
                    SessionApiKey = state.SessionApiKey,
                    Url = state.Url,
                    CreatedAt = state.CreatedAt,
                    LastUpdatedAt = state.LastUpdatedAt
                });
            }
        }

        public Task<RuntimeConversationConfigDto?> GetConfigurationAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationConfigDto?>(null);
            }

            lock (state.SyncRoot)
            {
                return Task.FromResult<RuntimeConversationConfigDto?>(new RuntimeConversationConfigDto
                {
                    RuntimeId = state.RuntimeId,
                    SessionId = state.SessionId
                });
            }
        }

        public Task<RuntimeConversationVscodeUrlDto?> GetVscodeUrlAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationVscodeUrlDto?>(null);
            }

            lock (state.SyncRoot)
            {
                return Task.FromResult<RuntimeConversationVscodeUrlDto?>(new RuntimeConversationVscodeUrlDto
                {
                    VscodeUrl = state.VscodeUrl
                });
            }
        }

        public Task<RuntimeConversationWebHostsDto?> GetWebHostsAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationWebHostsDto?>(null);
            }

            lock (state.SyncRoot)
            {
                return Task.FromResult<RuntimeConversationWebHostsDto?>(new RuntimeConversationWebHostsDto
                {
                    Hosts = new Dictionary<string, string>(state.WebHosts, StringComparer.OrdinalIgnoreCase)
                });
            }
        }

        public Task<RuntimeConversationMicroagentsResult?> GetMicroagentsAsync(string id)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationMicroagentsResult?>(null);
            }

            List<RuntimeMicroagentDto> microagents;
            lock (state.SyncRoot)
            {
                microagents = state.Microagents
                    .Select(agent => agent.ToDto())
                    .ToList();
            }

            return Task.FromResult<RuntimeConversationMicroagentsResult?>(new RuntimeConversationMicroagentsResult
            {
                Microagents = microagents
            });
        }

        public Task<RuntimeConversationEventsPageDto?> GetEventsAsync(string id, int startId, int? endId, bool reverse, int? limit)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationEventsPageDto?>(null);
            }

            List<RuntimeConversationEventDto> events;
            bool hasMore = false;

            lock (state.SyncRoot)
            {
                IEnumerable<ConversationEvent> query = state.Events.Where(evt => evt.EventId >= Math.Max(0, startId));

                if (endId.HasValue)
                {
                    query = query.Where(evt => evt.EventId <= endId.Value);
                }

                List<ConversationEvent> materialized = query.ToList();

                if (reverse)
                {
                    materialized.Sort((a, b) => b.EventId.CompareTo(a.EventId));
                }
                else
                {
                    materialized.Sort((a, b) => a.EventId.CompareTo(b.EventId));
                }

                if (limit.HasValue && limit.Value > 0)
                {
                    int boundedLimit = Math.Clamp(limit.Value, 1, 100);
                    if (materialized.Count > boundedLimit)
                    {
                        hasMore = true;
                        materialized = materialized.Take(boundedLimit).ToList();
                    }
                }

                events = materialized
                    .Select(evt => evt.ToDto())
                    .ToList();
            }

            return Task.FromResult<RuntimeConversationEventsPageDto?>(new RuntimeConversationEventsPageDto
            {
                Events = events,
                HasMore = hasMore
            });
        }

        public Task<RuntimeConversationEventDto?> AppendEventAsync(string id, JsonElement payload)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return Task.FromResult<RuntimeConversationEventDto?>(null);
            }

            Dictionary<string, JsonElement> additional = ExtractAdditionalData(payload);
            string type = ResolveEventType(additional);

            if (additional.ContainsKey("type"))
            {
                additional.Remove("type");
            }

            RuntimeConversationEventDto evt;
            lock (state.SyncRoot)
            {
                evt = state.AddEvent(_timeProvider.GetUtcNow(), type, additional);
            }

            _logger.LogInformation("Event appended to conversation {ConversationId}: {Type}", id, type);

            return Task.FromResult<RuntimeConversationEventDto?>(evt);
        }

        public async Task<IReadOnlyList<string>?> ListFilesAsync(string id, string? path, CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            return await _workspace.ListFilesAsync(path, state.WorkspaceRoot, cancellationToken).ConfigureAwait(false);
        }

        public async Task<RuntimeConversationFileSelectionResult?> SelectFileAsync(
            string id,
            string file,
            CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            WorkspaceFileSelection selection = await _workspace
                .ReadFileAsync(file, state.WorkspaceRoot, cancellationToken)
                .ConfigureAwait(false);

            if (selection.IsBinary)
            {
                return new RuntimeConversationFileSelectionResult
                {
                    IsBinary = true,
                    Error = selection.Error ?? "Binary files cannot be previewed."
                };
            }

            if (!string.IsNullOrWhiteSpace(selection.Error))
            {
                return new RuntimeConversationFileSelectionResult
                {
                    Error = selection.Error
                };
            }

            return new RuntimeConversationFileSelectionResult
            {
                Code = selection.Content
            };
        }

        public async Task<RuntimeConversationUploadResult?> UploadFilesAsync(
            string id,
            IReadOnlyList<RuntimeUploadedFile> files,
            CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            WorkspaceUploadResult result = await _workspace
                .UploadFilesAsync(files, state.WorkspaceRoot, cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeConversationUploadResult
            {
                UploadedFiles = result.UploadedFiles,
                SkippedFiles = result.SkippedFiles
            };
        }

        public async Task<RuntimeZipStreamResult?> ZipWorkspaceAsync(string id, CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            return await _workspace.ZipWorkspaceAsync(state.WorkspaceRoot, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<RuntimeGitChangeResult>?> GetGitChangesAsync(
            string id,
            CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            return await _gitClient
                .GetChangesAsync(state.WorkspaceRoot, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<RuntimeGitDiffResult?> GetGitDiffAsync(
            string id,
            string path,
            CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            WorkspaceFileSelection selection = await _workspace
                .ReadFileAsync(path, state.WorkspaceRoot, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(selection.Error) && !selection.IsBinary)
            {
                return null;
            }

            string? modified = selection.IsBinary ? null : selection.Content;
            string? original = await _gitClient
                .GetFileContentAsync(state.WorkspaceRoot, path, cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeGitDiffResult
            {
                Original = original,
                Modified = modified
            };
        }

        public async Task<RuntimeFileEditResponseDto?> EditFileAsync(
            string id,
            RuntimeFileEditRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetState(id, out ConversationState? state))
            {
                return null;
            }

            RuntimeFileEditResponseDto result = await _fileEditor
                .ExecuteAsync(request, state.WorkspaceRoot, state.LintEnabled, cancellationToken)
                .ConfigureAwait(false);

            if (result.LintEnabled.HasValue)
            {
                lock (state.SyncRoot)
                {
                    state.LintEnabled = result.LintEnabled.Value;
                }
            }

            return result;
        }

        private static void PopulateHostMetadata(ConversationState state)
        {
            state.WebHosts.Clear();
            state.WebHosts["workspace"] = $"https://workspace.local/{state.Id}";
            state.WebHosts["preview"] = $"https://preview.local/{state.Id}";
            state.WebHosts["service-api"] = $"https://api.local/conversations/{state.Id}";
        }

        private static void PopulateMicroagents(ConversationState state, CreateConversationRequestDto request)
        {
            state.Microagents.Clear();
            foreach (RuntimeMicroagentDefinition definition in CreateDefaultMicroagents(state.Id, request.Name))
            {
                state.Microagents.Add(definition);
            }
        }

        private static IEnumerable<RuntimeMicroagentDefinition> CreateDefaultMicroagents(string conversationId, string? conversationName)
        {
            string displayName = string.IsNullOrWhiteSpace(conversationName)
                ? conversationId
                : conversationName!;

            return new[]
            {
                new RuntimeMicroagentDefinition(
                    Name: "repo-overview",
                    Type: "prompt",
                    Content: $"Summarize repository context and goals for {displayName}.",
                    Triggers: new[] { "repo", "overview" },
                    Inputs: new[]
                    {
                        new RuntimeMicroagentInputDefinition("repository", "Repository or workspace name"),
                        new RuntimeMicroagentInputDefinition("goal", "Primary objective for the conversation")
                    },
                    Tools: new[] { "filesystem", "git" }),
                new RuntimeMicroagentDefinition(
                    Name: "test-runner",
                    Type: "workflow",
                    Content: $"Run relevant tests for {displayName} and report diagnostics.",
                    Triggers: new[] { "tests", "ci" },
                    Inputs: new[]
                    {
                        new RuntimeMicroagentInputDefinition("command", "Test command to execute"),
                        new RuntimeMicroagentInputDefinition("working_directory", "Directory where the command should run")
                    },
                    Tools: new[] { "shell", "process" })
            };
        }

        private static Dictionary<string, JsonElement> ExtractAdditionalData(JsonElement payload)
        {
            var data = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            if (payload.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in payload.EnumerateObject())
                {
                    data[property.Name] = property.Value.Clone();
                }
            }
            else if (payload.ValueKind != JsonValueKind.Undefined && payload.ValueKind != JsonValueKind.Null)
            {
                data["value"] = payload.Clone();
            }

            return data;
        }

        private static string ResolveEventType(Dictionary<string, JsonElement> additional)
        {
            if (additional.TryGetValue("type", out JsonElement explicitType) &&
                explicitType.ValueKind == JsonValueKind.String)
            {
                return explicitType.GetString() ?? "event";
            }

            if (additional.TryGetValue("action", out JsonElement action) && action.ValueKind == JsonValueKind.String)
            {
                return action.GetString() ?? "event";
            }

            if (additional.TryGetValue("observation", out JsonElement observation) && observation.ValueKind == JsonValueKind.String)
            {
                return observation.GetString() ?? "event";
            }

            if (additional.TryGetValue("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
            {
                return "message";
            }

            return "event";
        }

        private static string GenerateSessionApiKey()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private bool TryGetState(string id, out ConversationState? state)
        {
            return _conversations.TryGetValue(id, out state);
        }

        private sealed class ConversationState
        {
            private int _nextEventId = 1;

            public ConversationState(string id, DateTimeOffset createdAt, string workspaceRoot)
            {
                Id = id;
                CreatedAt = createdAt;
                LastUpdatedAt = createdAt;
                WorkspaceRoot = workspaceRoot;
            }

            public string Id { get; }

            public string WorkspaceRoot { get; }

            public string ConversationStatus { get; set; } = "CREATED";

            public string RuntimeStatus { get; set; } = "READY";

            public string StatusMessage { get; set; } = string.Empty;

            public string? SessionApiKey { get; set; }

            public string? RuntimeId { get; set; }

            public string? SessionId { get; set; }

            public string? Url { get; set; }

            public string? VscodeUrl { get; set; }

            public Dictionary<string, string> WebHosts { get; } = new(StringComparer.OrdinalIgnoreCase);

            public List<RuntimeMicroagentDefinition> Microagents { get; } = new();

            public bool LintEnabled { get; set; } = true;

            public DateTimeOffset CreatedAt { get; }

            public DateTimeOffset LastUpdatedAt { get; private set; }

            public List<ConversationEvent> Events { get; } = new();

            public object SyncRoot { get; } = new();

            public void Touch(DateTimeOffset timestamp)
            {
                LastUpdatedAt = timestamp;
            }

            public RuntimeConversationEventDto AddEvent(DateTimeOffset timestamp, string type, Dictionary<string, JsonElement> additional)
            {
                int eventId = _nextEventId++;

                var stored = new ConversationEvent(eventId, timestamp, type, CloneAdditionalData(additional));

                Events.Add(stored);
                Touch(timestamp);

                return stored.ToDto();
            }
        }

        private sealed class ConversationEvent
        {
            public ConversationEvent(int eventId, DateTimeOffset timestamp, string type, Dictionary<string, JsonElement> additional)
            {
                EventId = eventId;
                Timestamp = timestamp;
                Type = type;
                AdditionalData = additional;
            }

            public int EventId { get; }

            public DateTimeOffset Timestamp { get; }

            public string Type { get; }

            public Dictionary<string, JsonElement> AdditionalData { get; }

            public RuntimeConversationEventDto ToDto()
            {
                return new RuntimeConversationEventDto
                {
                    EventId = EventId,
                    CreatedAt = Timestamp,
                    Type = Type,
                    AdditionalData = CloneAdditionalData(AdditionalData)
                };
            }
        }

        private sealed record RuntimeMicroagentDefinition(
            string Name,
            string Type,
            string Content,
            IReadOnlyList<string> Triggers,
            IReadOnlyList<RuntimeMicroagentInputDefinition> Inputs,
            IReadOnlyList<string> Tools)
        {
            public RuntimeMicroagentDto ToDto()
            {
                return new RuntimeMicroagentDto
                {
                    Name = Name,
                    Type = Type,
                    Content = Content,
                    Triggers = Triggers.ToArray(),
                    Inputs = Inputs.Select(input => input.ToDto()).ToList(),
                    Tools = Tools.ToArray()
                };
            }
        }

        private sealed record RuntimeMicroagentInputDefinition(string Name, string Description)
        {
            public RuntimeMicroagentInputDto ToDto()
            {
                return new RuntimeMicroagentInputDto
                {
                    Name = Name,
                    Description = Description
                };
            }
        }

        private static Dictionary<string, JsonElement> CloneAdditionalData(Dictionary<string, JsonElement> source)
        {
            var clone = new Dictionary<string, JsonElement>(source.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var pair in source)
            {
                clone[pair.Key] = pair.Value.Clone();
            }

            return clone;
        }
    }
}
