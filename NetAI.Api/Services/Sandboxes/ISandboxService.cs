using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public interface ISandboxService
{
    Task<SandboxPageDto> SearchSandboxesAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task<SandboxInfoDto> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SandboxInfoDto>> BatchGetSandboxesAsync(
        IReadOnlyList<string> sandboxIds,
        CancellationToken cancellationToken);

    Task<SandboxInfoDto> StartSandboxAsync(
        StartSandboxRequestDto request,
        CancellationToken cancellationToken);

    Task<bool> PauseSandboxAsync(string sandboxId, CancellationToken cancellationToken);

    Task<bool> ResumeSandboxAsync(string sandboxId, CancellationToken cancellationToken);

    Task<bool> DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken);
}
