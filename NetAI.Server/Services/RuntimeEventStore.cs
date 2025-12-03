using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using NetAI.Server.Models;

namespace NetAI.Server.Services;

public sealed class RuntimeEventStore
{
    private readonly ConcurrentDictionary<string, RuntimeConversationState> _state = new();

    private readonly WorkspaceDirectoryProvider _workspaceDirectory;

    public RuntimeEventStore(WorkspaceDirectoryProvider workspaceDirectory)
    {
        _workspaceDirectory = workspaceDirectory;
    }

    public RuntimeConversationState Create(string? workspacePath)
    {
        workspacePath = string.IsNullOrWhiteSpace(workspacePath)
            ? _workspaceDirectory.WorkspacePath
            : Path.GetFullPath(workspacePath);

        Directory.CreateDirectory(workspacePath);

        var id = Guid.NewGuid().ToString("N");
        var state = new RuntimeConversationState
        {
            ConversationId = id,
            WorkspacePath = workspacePath,
            RuntimeStatus = "ready",
            ConversationStatus = "ready",
            SessionApiKey = GenerateSessionApiKey()
        };

        _state[id] = state;
        return state;
    }

    public RuntimeConversationState? Get(string id)
    {
        _state.TryGetValue(id, out var state);
        return state;
    }

    public void EnsureRuntimeIdentifiers(RuntimeConversationState state)
    {
        state.RuntimeId ??= Guid.NewGuid().ToString("N");
        state.SessionId ??= Guid.NewGuid().ToString("N");
    }

    public RuntimeConversationEvent AppendEvent(string id, string type, JsonElement payload)
    {
        var state = Get(id) ?? throw new InvalidOperationException($"Conversation {id} not found.");
        var evt = new RuntimeConversationEvent
        {
            Id = state.Events.Count + 1,
            Type = type,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        state.Events.Add(evt);
        return evt;
    }

    private static string GenerateSessionApiKey()
    {
        Span<byte> buffer = stackalloc byte[24];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
