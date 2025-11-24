using Microsoft.AspNetCore.Http;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Models.Git;
using System.Text.Json;

namespace NetAI.Api.Services.Conversations;

public interface IConversationSessionService
{
    Task<ResultSetDto<ConversationDto>> GetConversationsAsync(
        int limit,
        string pageId,
        string selectedRepository,
        string conversationTrigger,
        CancellationToken cancellationToken);

    Task<ConversationDto> GetConversationAsync(string conversationId, CancellationToken cancellationToken);

    Task<ConversationDto> CreateConversationAsync(CreateConversationRequestDto request, CancellationToken cancellationToken);

    Task<bool> UpdateConversationAsync(string conversationId, UpdateConversationRequestDto request, CancellationToken cancellationToken);

    Task<bool> DeleteConversationAsync(string conversationId, CancellationToken cancellationToken);

    Task<ConversationResponseDto> StartConversationAsync(
        string conversationId,
        string sessionApiKey,
        IEnumerable<string> providers,
        CancellationToken cancellationToken);

    Task<ConversationResponseDto> StopConversationAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<bool> AddMessageAsync(
        string conversationId,
        string sessionApiKey,
        ConversationMessageRequestDto request,
        CancellationToken cancellationToken);

    Task AddEventAsync(
        string conversationId,
        string sessionApiKey,
        JsonElement eventPayload,
        CancellationToken cancellationToken);

    Task<ConversationEventsPageDto> GetEventsAsync(
        string conversationId,
        string sessionApiKey,
        int startId,
        int? endId,
        bool reverse,
        int limit,
        bool excludeHidden,
        CancellationToken cancellationToken);

    Task<TrajectoryResponseDto> GetTrajectoryAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<RuntimeConfigResponseDto> GetRuntimeConfigAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<VSCodeUrlResponseDto> GetVSCodeUrlAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<WebHostsResponseDto> GetWebHostsAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<string> GetSecurityAnalyzerUrlAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<GetMicroagentsResponseDto> GetMicroagentsAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<GetMicroagentPromptResponseDto> GetRememberPromptAsync(
        string conversationId,
        string sessionApiKey,
        int eventId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListFilesAsync(
        string conversationId,
        string sessionApiKey,
        string path,
        CancellationToken cancellationToken);

    Task<FileUploadSuccessResponseDto> UploadFilesAsync(
        string conversationId,
        string sessionApiKey,
        IEnumerable<IFormFile> files,
        CancellationToken cancellationToken);

    Task<FileSelectionResultDto> SelectFileAsync(
        string conversationId,
        string sessionApiKey,
        string file,
        CancellationToken cancellationToken);

    Task<WorkspaceZipStreamDto> ZipWorkspaceAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitChangeDto>> GetGitChangesAsync(
        string conversationId,
        string sessionApiKey,
        CancellationToken cancellationToken);

    Task<GitChangeDiffDto> GetGitDiffAsync(
        string conversationId,
        string sessionApiKey,
        string path,
        CancellationToken cancellationToken);

    Task<FeedbackResponseDto> SubmitFeedbackAsync(
        string conversationId,
        string sessionApiKey,
        FeedbackDto feedback,
        CancellationToken cancellationToken);
}
