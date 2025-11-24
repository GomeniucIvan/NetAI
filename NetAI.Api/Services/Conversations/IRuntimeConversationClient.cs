using System.IO;
using System.Text.Json;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;

public interface IRuntimeConversationClient
{
    Task<RuntimeConversationHandle> AttachAsync(
        ConversationMetadataRecord conversation,
        CancellationToken cancellationToken);

    Task DetachAsync(RuntimeConversationHandle handle, CancellationToken cancellationToken);

    Task StartAsync(
        RuntimeConversationHandle handle,
        IEnumerable<string> providers,
        CancellationToken cancellationToken);

    Task StopAsync(RuntimeConversationHandle handle, CancellationToken cancellationToken);

    Task<bool> SendMessageAsync(
        RuntimeConversationHandle handle,
        string message,
        CancellationToken cancellationToken);

    Task SendEventAsync(
        RuntimeConversationHandle handle,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<ConversationEventsPageDto> GetEventsAsync(
        RuntimeConversationHandle handle,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        bool excludeHidden,
        CancellationToken cancellationToken);

    Task<TrajectoryResponseDto> GetTrajectoryAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);

    Task<RuntimeConfigResponseDto> GetRuntimeConfigAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);

    Task<VSCodeUrlResponseDto> GetVSCodeUrlAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);

    Task<WebHostsResponseDto> GetWebHostsAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);

    Task<string> GetSecurityAnalyzerUrlAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroagentDto>> GetMicroagentsAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListFilesAsync(
        RuntimeConversationHandle handle,
        string path,
        CancellationToken cancellationToken);

    Task<RuntimeObservation> RunActionAsync(
        RuntimeConversationHandle handle,
        IRuntimeAction action,
        CancellationToken cancellationToken);

    Task<RuntimeZipStreamResult> ZipWorkspaceAsync(
        RuntimeConversationHandle handle,
        CancellationToken cancellationToken);
}
