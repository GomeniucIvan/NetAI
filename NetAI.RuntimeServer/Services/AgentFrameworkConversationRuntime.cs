using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services
{
    public class AgentFrameworkConversationRuntime : IConversationRuntime
    {
        private readonly InMemoryConversationRuntime _innerRuntime;
        private readonly IAgentFrameworkClient _agentClient;
        private readonly AgentRuntimeOptions _options;
        private readonly ILogger<AgentFrameworkConversationRuntime> _logger;
        private readonly ConcurrentDictionary<string, AgentConversationMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);

        public AgentFrameworkConversationRuntime(
            InMemoryConversationRuntime innerRuntime,
            IAgentFrameworkClient agentClient,
            IOptions<AgentRuntimeOptions> options,
            ILogger<AgentFrameworkConversationRuntime> logger)
        {
            _innerRuntime = innerRuntime;
            _agentClient = agentClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<RuntimeConversationInitResult> InitializeAsync(CreateConversationRequestDto request)
        {
            RuntimeConversationInitResult baseResult = await _innerRuntime.InitializeAsync(request);

            AgentConversationSession session;
            try
            {
                session = await _agentClient.CreateConversationAsync(baseResult, request, _options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent-backed conversation {ConversationId}", baseResult.ConversationId);
                return baseResult;
            }

            var metadata = new AgentConversationMetadata(session.WebHosts, session.Microagents, session.ConversationStatus, session.RuntimeStatus);
            _metadata[baseResult.ConversationId] = metadata;

            if (session.InitialEvents.Count > 0)
            {
                await AppendAgentEventsAsync(baseResult.ConversationId, session.InitialEvents, CancellationToken.None);
            }

            metadata.UpdateStatus(session.ConversationStatus, session.RuntimeStatus);

            return new RuntimeConversationInitResult
            {
                Status = baseResult.Status,
                ConversationId = baseResult.ConversationId,
                ConversationStatus = session.ConversationStatus ?? baseResult.ConversationStatus,
                RuntimeStatus = session.RuntimeStatus ?? baseResult.RuntimeStatus,
                Message = string.IsNullOrWhiteSpace(session.Message) ? baseResult.Message : session.Message,
                SessionApiKey = baseResult.SessionApiKey,
                RuntimeId = baseResult.RuntimeId,
                SessionId = baseResult.SessionId
            };
        }

        public async Task<RuntimeConversationOperationResult?> StartAsync(string id)
        {
            RuntimeConversationOperationResult? baseResult = await _innerRuntime.StartAsync(id);
            if (baseResult is null)
            {
                return null;
            }

            AgentOperationResult? agentResult = await InvokeSafely(() => _agentClient.StartConversationAsync(id, CancellationToken.None));
            if (agentResult is null)
            {
                return baseResult;
            }

            if (agentResult.Events.Count > 0)
            {
                await AppendAgentEventsAsync(id, agentResult.Events, CancellationToken.None);
            }

            UpdateMetadata(id, agentResult.ConversationStatus, agentResult.RuntimeStatus, null, null);

            return new RuntimeConversationOperationResult
            {
                Status = baseResult.Status,
                ConversationId = baseResult.ConversationId,
                ConversationStatus = agentResult.ConversationStatus ?? baseResult.ConversationStatus,
                RuntimeStatus = agentResult.RuntimeStatus ?? baseResult.RuntimeStatus,
                Message = string.IsNullOrWhiteSpace(agentResult.Message) ? baseResult.Message : agentResult.Message,
                SessionApiKey = baseResult.SessionApiKey,
                RuntimeId = baseResult.RuntimeId,
                SessionId = baseResult.SessionId
            };
        }

        public async Task<RuntimeConversationOperationResult?> StopAsync(string id)
        {
            RuntimeConversationOperationResult? baseResult = await _innerRuntime.StopAsync(id);
            if (baseResult is null)
            {
                return null;
            }

            AgentOperationResult? agentResult = await InvokeSafely(() => _agentClient.StopConversationAsync(id, CancellationToken.None));
            if (agentResult is null)
            {
                return baseResult;
            }

            if (agentResult.Events.Count > 0)
            {
                await AppendAgentEventsAsync(id, agentResult.Events, CancellationToken.None);
            }

            UpdateMetadata(id, agentResult.ConversationStatus, agentResult.RuntimeStatus, null, null);

            return new RuntimeConversationOperationResult
            {
                Status = baseResult.Status,
                ConversationId = baseResult.ConversationId,
                ConversationStatus = agentResult.ConversationStatus ?? baseResult.ConversationStatus,
                RuntimeStatus = agentResult.RuntimeStatus ?? baseResult.RuntimeStatus,
                Message = string.IsNullOrWhiteSpace(agentResult.Message) ? baseResult.Message : agentResult.Message,
                SessionApiKey = baseResult.SessionApiKey,
                RuntimeId = baseResult.RuntimeId,
                SessionId = baseResult.SessionId
            };
        }

        public async Task<RuntimeConversationEventDto?> AppendMessageAsync(string id, string message, string? source = null)
        {
            RuntimeConversationEventDto? userEvent = await _innerRuntime.AppendMessageAsync(id, message, source);
            if (userEvent is null)
            {
                return null;
            }

            AgentMessageResult? agentResult = await InvokeSafely(() => _agentClient.SendMessageAsync(id, message, CancellationToken.None));
            if (agentResult is null)
            {
                return userEvent;
            }

            if (agentResult.Events.Count > 0)
            {
                await AppendAgentEventsAsync(id, agentResult.Events, CancellationToken.None);
            }

            UpdateMetadata(id, agentResult.ConversationStatus, agentResult.RuntimeStatus, null, null);

            if (agentResult.IsTerminal)
            {
                await _innerRuntime.StopAsync(id);
            }

            return userEvent;
        }

        public async Task<RuntimeConversationStateDto?> GetConversationAsync(string id)
        {
            RuntimeConversationStateDto? state = await _innerRuntime.GetConversationAsync(id);
            if (state is null)
            {
                return null;
            }

            if (_metadata.TryGetValue(id, out AgentConversationMetadata? metadata))
            {
                AgentConversationSnapshot snapshot = metadata.Snapshot();
                return new RuntimeConversationStateDto
                {
                    ConversationId = state.ConversationId,
                    Status = snapshot.ConversationStatus ?? state.Status,
                    RuntimeStatus = snapshot.RuntimeStatus ?? state.RuntimeStatus,
                    SessionApiKey = state.SessionApiKey,
                    Url = state.Url,
                    CreatedAt = state.CreatedAt,
                    LastUpdatedAt = state.LastUpdatedAt
                };
            }

            return state;
        }

        public Task<RuntimeConversationConfigDto?> GetConfigurationAsync(string id)
        {
            return _innerRuntime.GetConfigurationAsync(id);
        }

        public Task<RuntimeConversationVscodeUrlDto?> GetVscodeUrlAsync(string id)
        {
            return _innerRuntime.GetVscodeUrlAsync(id);
        }

        public async Task<RuntimeConversationWebHostsDto?> GetWebHostsAsync(string id)
        {
            if (_metadata.TryGetValue(id, out AgentConversationMetadata? metadata))
            {
                AgentConversationSnapshot snapshot = metadata.Snapshot();
                return new RuntimeConversationWebHostsDto
                {
                    Hosts = snapshot.WebHosts
                };
            }

            IReadOnlyDictionary<string, string> hosts = await InvokeSafely(() => _agentClient.GetWebHostsAsync(id, CancellationToken.None))
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (hosts.Count > 0)
            {
                _metadata.AddOrUpdate(id,
                    _ => new AgentConversationMetadata(hosts, Array.Empty<RuntimeMicroagentDto>(), null, null),
                    (_, existing) =>
                    {
                        existing.UpdateMetadata(hosts, null);
                        return existing;
                    });

                return new RuntimeConversationWebHostsDto
                {
                    Hosts = new Dictionary<string, string>(hosts, StringComparer.OrdinalIgnoreCase)
                };
            }

            return await _innerRuntime.GetWebHostsAsync(id);
        }

        public async Task<RuntimeConversationMicroagentsResult?> GetMicroagentsAsync(string id)
        {
            if (_metadata.TryGetValue(id, out AgentConversationMetadata? metadata))
            {
                AgentConversationSnapshot snapshot = metadata.Snapshot();
                if (snapshot.Microagents.Count > 0)
                {
                    return new RuntimeConversationMicroagentsResult
                    {
                        Microagents = snapshot.Microagents
                    };
                }
            }

            IReadOnlyList<RuntimeMicroagentDto> microagents = await InvokeSafely(() => _agentClient.GetMicroagentsAsync(id, CancellationToken.None))
                ?? Array.Empty<RuntimeMicroagentDto>();

            if (microagents.Count > 0)
            {
                _metadata.AddOrUpdate(id,
                    _ => new AgentConversationMetadata(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), microagents, null, null),
                    (_, existing) =>
                    {
                        existing.UpdateMetadata(null, microagents);
                        return existing;
                    });

                return new RuntimeConversationMicroagentsResult
                {
                    Microagents = microagents
                };
            }

            return await _innerRuntime.GetMicroagentsAsync(id);
        }

        public Task<RuntimeConversationEventsPageDto?> GetEventsAsync(string id, int startId, int? endId, bool reverse, int? limit)
        {
            return _innerRuntime.GetEventsAsync(id, startId, endId, reverse, limit);
        }

        public Task<RuntimeConversationEventDto?> AppendEventAsync(string id, JsonElement payload)
        {
            return _innerRuntime.AppendEventAsync(id, payload);
        }

        public Task<IReadOnlyList<string>?> ListFilesAsync(string id, string? path, CancellationToken cancellationToken)
        {
            return _innerRuntime.ListFilesAsync(id, path, cancellationToken);
        }

        public Task<RuntimeConversationFileSelectionResult?> SelectFileAsync(string id, string file, CancellationToken cancellationToken)
        {
            return _innerRuntime.SelectFileAsync(id, file, cancellationToken);
        }

        public Task<RuntimeConversationUploadResult?> UploadFilesAsync(string id, IReadOnlyList<RuntimeUploadedFile> files, CancellationToken cancellationToken)
        {
            return _innerRuntime.UploadFilesAsync(id, files, cancellationToken);
        }

        public Task<RuntimeZipStreamResult?> ZipWorkspaceAsync(string id, CancellationToken cancellationToken)
        {
            return _innerRuntime.ZipWorkspaceAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<RuntimeGitChangeResult>?> GetGitChangesAsync(string id, CancellationToken cancellationToken)
        {
            return _innerRuntime.GetGitChangesAsync(id, cancellationToken);
        }

        public Task<RuntimeGitDiffResult?> GetGitDiffAsync(string id, string path, CancellationToken cancellationToken)
        {
            return _innerRuntime.GetGitDiffAsync(id, path, cancellationToken);
        }

        public Task<RuntimeFileEditResponseDto?> EditFileAsync(
            string id,
            RuntimeFileEditRequestDto request,
            CancellationToken cancellationToken)
        {
            return _innerRuntime.EditFileAsync(id, request, cancellationToken);
        }

        private async Task AppendAgentEventsAsync(string id, IReadOnlyList<AgentFrameworkEvent> events, CancellationToken cancellationToken)
        {
            foreach (AgentFrameworkEvent evt in events)
            {
                JsonElement payload = evt.ToJsonElement();
                await _innerRuntime.AppendEventAsync(id, payload);
            }
        }

        private void UpdateMetadata(
            string id,
            string? conversationStatus,
            string? runtimeStatus,
            IReadOnlyDictionary<string, string>? hosts,
            IReadOnlyList<RuntimeMicroagentDto>? microagents)
        {
            _metadata.AddOrUpdate(
                id,
                _ => new AgentConversationMetadata(hosts ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), microagents ?? Array.Empty<RuntimeMicroagentDto>(), conversationStatus, runtimeStatus),
                (_, existing) =>
                {
                    existing.UpdateStatus(conversationStatus, runtimeStatus);
                    existing.UpdateMetadata(hosts, microagents);
                    return existing;
                });
        }

        private async Task<T?> InvokeSafely<T>(Func<Task<T>> factory)
        {
            try
            {
                return await factory();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent framework operation failed");
                return default;
            }
        }

        private sealed class AgentConversationMetadata
        {
            private readonly object _syncRoot = new();
            private Dictionary<string, string> _webHosts;
            private IReadOnlyList<RuntimeMicroagentDto> _microagents;
            private string? _conversationStatus;
            private string? _runtimeStatus;

            public AgentConversationMetadata(
                IReadOnlyDictionary<string, string> webHosts,
                IReadOnlyList<RuntimeMicroagentDto> microagents,
                string? conversationStatus,
                string? runtimeStatus)
            {
                _webHosts = new Dictionary<string, string>(webHosts, StringComparer.OrdinalIgnoreCase);
                _microagents = microagents;
                _conversationStatus = conversationStatus;
                _runtimeStatus = runtimeStatus;
            }

            public void UpdateMetadata(IReadOnlyDictionary<string, string>? webHosts, IReadOnlyList<RuntimeMicroagentDto>? microagents)
            {
                lock (_syncRoot)
                {
                    if (webHosts is not null && webHosts.Count > 0)
                    {
                        _webHosts = new Dictionary<string, string>(webHosts, StringComparer.OrdinalIgnoreCase);
                    }

                    if (microagents is not null && microagents.Count > 0)
                    {
                        _microagents = microagents;
                    }
                }
            }

            public void UpdateStatus(string? conversationStatus, string? runtimeStatus)
            {
                lock (_syncRoot)
                {
                    if (!string.IsNullOrWhiteSpace(conversationStatus))
                    {
                        _conversationStatus = conversationStatus;
                    }

                    if (!string.IsNullOrWhiteSpace(runtimeStatus))
                    {
                        _runtimeStatus = runtimeStatus;
                    }
                }
            }

            public AgentConversationSnapshot Snapshot()
            {
                lock (_syncRoot)
                {
                    return new AgentConversationSnapshot(
                        new Dictionary<string, string>(_webHosts, StringComparer.OrdinalIgnoreCase),
                        _microagents,
                        _conversationStatus,
                        _runtimeStatus);
                }
            }
        }

        private sealed record AgentConversationSnapshot(
            IReadOnlyDictionary<string, string> WebHosts,
            IReadOnlyList<RuntimeMicroagentDto> Microagents,
            string? ConversationStatus,
            string? RuntimeStatus);
    }
}
