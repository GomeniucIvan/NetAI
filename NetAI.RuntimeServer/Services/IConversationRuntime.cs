using System.Text.Json;
using System.Threading;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services
{
    public interface IConversationRuntime
    {
        Task<RuntimeConversationInitResult> InitializeAsync(CreateConversationRequestDto request);
        Task<RuntimeConversationOperationResult?> StartAsync(string id);
        Task<RuntimeConversationOperationResult?> StopAsync(string id);
        Task<RuntimeConversationEventDto?> AppendMessageAsync(string id, string message, string? source = null);
        Task<RuntimeConversationStateDto?> GetConversationAsync(string id);
        Task<RuntimeConversationConfigDto?> GetConfigurationAsync(string id);
        Task<RuntimeConversationVscodeUrlDto?> GetVscodeUrlAsync(string id);
        Task<RuntimeConversationWebHostsDto?> GetWebHostsAsync(string id);
        Task<RuntimeConversationMicroagentsResult?> GetMicroagentsAsync(string id);
        Task<RuntimeConversationEventsPageDto?> GetEventsAsync(string id, int startId, int? endId, bool reverse, int? limit);
        Task<RuntimeConversationEventDto?> AppendEventAsync(string id, JsonElement payload);
        Task<IReadOnlyList<string>?> ListFilesAsync(string id, string? path, CancellationToken cancellationToken);
        Task<RuntimeConversationFileSelectionResult?> SelectFileAsync(string id, string file, CancellationToken cancellationToken);
        Task<RuntimeConversationUploadResult?> UploadFilesAsync(string id, IReadOnlyList<RuntimeUploadedFile> files, CancellationToken cancellationToken);
        Task<RuntimeZipStreamResult?> ZipWorkspaceAsync(string id, CancellationToken cancellationToken);
        Task<IReadOnlyList<RuntimeGitChangeResult>?> GetGitChangesAsync(string id, CancellationToken cancellationToken);
        Task<RuntimeGitDiffResult?> GetGitDiffAsync(string id, string path, CancellationToken cancellationToken);
        Task<RuntimeFileEditResponseDto?> EditFileAsync(string id, RuntimeFileEditRequestDto request, CancellationToken cancellationToken);
    }
}
