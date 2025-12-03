using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services
{
    public sealed class StubAgentFrameworkClient : IAgentFrameworkClient
    {
        private readonly AgentRuntimeOptions _options;
        private readonly ILogger<StubAgentFrameworkClient> _logger;
        private readonly ConcurrentDictionary<string, StubConversation> _sessions = new(StringComparer.Ordinal);

        public StubAgentFrameworkClient(
            IOptions<AgentRuntimeOptions> options,
            ILogger<StubAgentFrameworkClient> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task<AgentConversationSession> CreateConversationAsync(
            RuntimeConversationInitResult initialState,
            CreateConversationRequestDto request,
            AgentRuntimeOptions options,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, string> hosts = BuildHosts();
            IReadOnlyList<RuntimeMicroagentDto> microagents = LoadMicroagents();

            var conversation = new StubConversation(hosts, microagents)
            {
                ConversationStatus = initialState.ConversationStatus,
                RuntimeStatus = initialState.RuntimeStatus
            };

            _sessions[initialState.ConversationId] = conversation;

            string message = $"Connected to {options.Provider} runtime";
            var initialEvents = new List<AgentFrameworkEvent>
            {
                AgentFrameworkEvent.FromDictionary("system", new Dictionary<string, object?>
                {
                    ["message"] = message,
                    ["source"] = "system"
                })
            };

            return Task.FromResult(new AgentConversationSession(
                initialState.ConversationId,
                message,
                hosts,
                microagents,
                initialEvents,
                conversation.ConversationStatus,
                conversation.RuntimeStatus));
        }

        public Task<AgentOperationResult> StartConversationAsync(string conversationId, CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(conversationId, out StubConversation? conversation))
            {
                throw new InvalidOperationException($"Conversation '{conversationId}' is not initialized.");
            }

            conversation.ConversationStatus = "STARTED";
            conversation.RuntimeStatus = "RUNNING";

            var events = new List<AgentFrameworkEvent>
            {
                AgentFrameworkEvent.FromDictionary("status", new Dictionary<string, object?>
                {
                    ["message"] = $"Agent {_options.Provider} session running",
                    ["source"] = "system"
                })
            };

            return Task.FromResult(new AgentOperationResult(
                $"Agent {_options.Provider} session started",
                conversation.ConversationStatus,
                conversation.RuntimeStatus,
                events));
        }

        public Task<AgentOperationResult> StopConversationAsync(string conversationId, CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(conversationId, out StubConversation? conversation))
            {
                throw new InvalidOperationException($"Conversation '{conversationId}' is not initialized.");
            }

            conversation.ConversationStatus = "STOPPED";
            conversation.RuntimeStatus = "IDLE";

            var events = new List<AgentFrameworkEvent>
            {
                AgentFrameworkEvent.FromDictionary("status", new Dictionary<string, object?>
                {
                    ["message"] = "Runtime stopped",
                    ["source"] = "system"
                })
            };

            return Task.FromResult(new AgentOperationResult(
                "Agent runtime stopped",
                conversation.ConversationStatus,
                conversation.RuntimeStatus,
                events));
        }

        public Task<AgentMessageResult> SendMessageAsync(string conversationId, string message, CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(conversationId, out StubConversation? conversation))
            {
                throw new InvalidOperationException($"Conversation '{conversationId}' is not initialized.");
            }

            conversation.RuntimeStatus = "RUNNING";

            string provider = _options.Provider;
            string sanitized = message.Trim();

            var events = new List<AgentFrameworkEvent>
            {
                AgentFrameworkEvent.FromDictionary("think", new Dictionary<string, object?>
                {
                    ["message"] = $"{provider} agent is considering: {sanitized}",
                    ["source"] = "agent"
                }),
                AgentFrameworkEvent.FromDictionary("run", new Dictionary<string, object?>
                {
                    ["tool"] = _options.EnableTooling ? "shell" : "planning",
                    ["command"] = _options.EnableTooling
                        ? $"echo \"{EscapeForShell(sanitized)}\""
                        : "No tool execution allowed",
                    ["source"] = "agent"
                }),
                AgentFrameworkEvent.FromDictionary("observation", new Dictionary<string, object?>
                {
                    ["result"] = $"Simulated output for '{sanitized}'",
                    ["source"] = "system"
                })
            };

            bool isTerminal = sanitized.Contains("done", StringComparison.OrdinalIgnoreCase);
            if (isTerminal)
            {
                conversation.ConversationStatus = "COMPLETED";
                conversation.RuntimeStatus = "IDLE";
                events.Add(AgentFrameworkEvent.FromDictionary("finish", new Dictionary<string, object?>
                {
                    ["message"] = "Conversation complete",
                    ["source"] = "agent"
                }));
            }
            else
            {
                conversation.RuntimeStatus = "IDLE";
            }

            string responseMessage = isTerminal
                ? "Agent run complete"
                : $"Agent {_options.Provider} processed the message";

            return Task.FromResult(new AgentMessageResult(
                events,
                conversation.ConversationStatus,
                conversation.RuntimeStatus,
                isTerminal,
                responseMessage));
        }

        public Task<IReadOnlyList<RuntimeMicroagentDto>> GetMicroagentsAsync(string conversationId, CancellationToken cancellationToken)
        {
            if (_sessions.TryGetValue(conversationId, out StubConversation? conversation))
            {
                return Task.FromResult<IReadOnlyList<RuntimeMicroagentDto>>(conversation.Microagents);
            }

            return Task.FromResult<IReadOnlyList<RuntimeMicroagentDto>>(Array.Empty<RuntimeMicroagentDto>());
        }

        public Task<IReadOnlyDictionary<string, string>> GetWebHostsAsync(string conversationId, CancellationToken cancellationToken)
        {
            if (_sessions.TryGetValue(conversationId, out StubConversation? conversation))
            {
                return Task.FromResult<IReadOnlyDictionary<string, string>>(conversation.WebHosts);
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        private IReadOnlyDictionary<string, string> BuildHosts()
        {
            var hosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["agent_dashboard"] = "http://localhost:4173"
            };

            if (_options.Provider.Equals("openhands", StringComparison.OrdinalIgnoreCase))
            {
                hosts["workspace"] = "http://localhost:3000";
            }

            return hosts;
        }

        private IReadOnlyList<RuntimeMicroagentDto> LoadMicroagents()
        {
            var list = new List<RuntimeMicroagentDto>();

            foreach (string path in _options.MicroagentRegistryPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                try
                {
                    using FileStream stream = File.OpenRead(fullPath);
                    using JsonDocument document = JsonDocument.Parse(stream);

                    if (document.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement element in document.RootElement.EnumerateArray())
                    {
                        string name = element.GetPropertyOrDefault("name", "microagent");
                        string type = element.GetPropertyOrDefault("type", "workflow");
                        string content = element.GetPropertyOrDefault("content", string.Empty);
                        IReadOnlyList<string> triggers = element.GetStringArrayOrDefault("triggers");
                        IReadOnlyList<RuntimeMicroagentInputDto> inputs = element.GetInputsOrDefault();
                        IReadOnlyList<string> tools = element.GetStringArrayOrDefault("tools");

                        list.Add(new RuntimeMicroagentDto
                        {
                            Name = name,
                            Type = type,
                            Content = content,
                            Triggers = triggers,
                            Inputs = inputs,
                            Tools = tools
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load microagents from {Path}", fullPath);
                }
            }

            if (list.Count == 0)
            {
                list.Add(new RuntimeMicroagentDto
                {
                    Name = "default",
                    Type = "workflow",
                    Content = "Simulated helper agent",
                    Triggers = new[] { "default" },
                    Inputs = Array.Empty<RuntimeMicroagentInputDto>(),
                    Tools = _options.EnableTooling
                        ? new[] { "shell", "filesystem" }
                        : new[] { "planner" }
                });
            }

            return list;
        }

        private static string EscapeForShell(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class StubConversation
        {
            public StubConversation(
                IReadOnlyDictionary<string, string> webHosts,
                IReadOnlyList<RuntimeMicroagentDto> microagents)
            {
                WebHosts = new Dictionary<string, string>(webHosts, StringComparer.OrdinalIgnoreCase);
                Microagents = microagents.ToList();
            }

            public Dictionary<string, string> WebHosts { get; }

            public List<RuntimeMicroagentDto> Microagents { get; }

            public string ConversationStatus { get; set; } = "CREATED";

            public string RuntimeStatus { get; set; } = "READY";
        }
    }

    internal static class JsonElementExtensions
    {
        public static string GetPropertyOrDefault(this JsonElement element, string name, string defaultValue)
        {
            return element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? defaultValue
                : defaultValue;
        }

        public static IReadOnlyList<string> GetStringArrayOrDefault(this JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.Array)
            {
                return property.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }

            return Array.Empty<string>();
        }

        public static IReadOnlyList<RuntimeMicroagentInputDto> GetInputsOrDefault(this JsonElement element)
        {
            if (element.TryGetProperty("inputs", out JsonElement property) && property.ValueKind == JsonValueKind.Array)
            {
                return property.EnumerateArray()
                    .Select(item => new RuntimeMicroagentInputDto
                    {
                        Name = item.GetPropertyOrDefault("name", string.Empty),
                        Description = item.GetPropertyOrDefault("description", string.Empty)
                    })
                    .ToList();
            }

            return Array.Empty<RuntimeMicroagentInputDto>();
        }
    }
}
