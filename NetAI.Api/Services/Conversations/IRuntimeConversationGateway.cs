using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;


public interface IRuntimeConversationGateway
{
    Task<RuntimeConversationInitResult> InitializeConversationAsync(
        RuntimeConversationInitRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeConversationOperationResult> StartConversationAsync(
        RuntimeConversationOperationRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeConversationOperationResult> StopConversationAsync(
        RuntimeConversationOperationRequest request,
        CancellationToken cancellationToken);

    Task PostEventAsync(RuntimeConversationEventRequest request, CancellationToken cancellationToken);

    Task PostMessageAsync(RuntimeConversationMessageRequest request, CancellationToken cancellationToken);

    Task<RuntimeConversationEventsResult> GetEventsAsync(
        RuntimeConversationEventsRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeConfigResponseDto> GetRuntimeConfigAsync(
        RuntimeConversationMetadataRequest request,
        CancellationToken cancellationToken);

    Task<VSCodeUrlResponseDto> GetVSCodeUrlAsync(
        RuntimeConversationMetadataRequest request,
        CancellationToken cancellationToken);

    Task<WebHostsResponseDto> GetWebHostsAsync(
        RuntimeConversationMetadataRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeConversationMicroagentsResult> GetMicroagentsAsync(
        RuntimeConversationMicroagentsRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListFilesAsync(
        RuntimeConversationFileListRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeConversationFileSelectionResult> SelectFileAsync(
        RuntimeConversationFileSelectionRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeConversationUploadResult> UploadFilesAsync(
        RuntimeConversationUploadRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeZipStreamResult> ZipWorkspaceAsync(
        RuntimeConversationZipRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RuntimeGitChangeResult>> GetGitChangesAsync(
        RuntimeConversationGitChangesRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeGitDiffResult> GetGitDiffAsync(
        RuntimeConversationGitDiffRequest request,
        CancellationToken cancellationToken);
}
