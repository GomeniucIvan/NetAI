using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services
{
    public interface IAgentFrameworkClient
    {
        Task<AgentConversationSession> CreateConversationAsync(
            RuntimeConversationInitResult initialState,
            CreateConversationRequestDto request,
            AgentRuntimeOptions options,
            CancellationToken cancellationToken);

        Task<AgentOperationResult> StartConversationAsync(string conversationId, CancellationToken cancellationToken);

        Task<AgentOperationResult> StopConversationAsync(string conversationId, CancellationToken cancellationToken);

        Task<AgentMessageResult> SendMessageAsync(string conversationId, string message, CancellationToken cancellationToken);

        Task<IReadOnlyList<RuntimeMicroagentDto>> GetMicroagentsAsync(string conversationId, CancellationToken cancellationToken);

        Task<IReadOnlyDictionary<string, string>> GetWebHostsAsync(string conversationId, CancellationToken cancellationToken);
    }

    public sealed record AgentFrameworkEvent(string Type, IReadOnlyDictionary<string, object?> AdditionalData)
    {
        public static AgentFrameworkEvent FromDictionary(string type, IDictionary<string, object?> additional)
        {
            return new AgentFrameworkEvent(type, new ReadOnlyDictionary<string, object?>(additional));
        }

        public JsonElement ToJsonElement()
        {
            var payload = new Dictionary<string, object?>(AdditionalData.Count + 2, StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = Type
            };

            foreach (var pair in AdditionalData)
            {
                payload[pair.Key] = pair.Value;
            }

            if (!payload.ContainsKey("source"))
            {
                payload["source"] = "agent";
            }

            return JsonSerializer.SerializeToElement(payload);
        }
    }

    public sealed record AgentConversationSession(
        string ConversationId,
        string Message,
        IReadOnlyDictionary<string, string> WebHosts,
        IReadOnlyList<RuntimeMicroagentDto> Microagents,
        IReadOnlyList<AgentFrameworkEvent> InitialEvents,
        string? ConversationStatus,
        string? RuntimeStatus);

    public sealed record AgentOperationResult(
        string Message,
        string? ConversationStatus,
        string? RuntimeStatus,
        IReadOnlyList<AgentFrameworkEvent> Events);

    public sealed record AgentMessageResult(
        IReadOnlyList<AgentFrameworkEvent> Events,
        string? ConversationStatus,
        string? RuntimeStatus,
        bool IsTerminal,
        string Message);
}
