using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public interface ISandboxSpecService
{
    Task<SandboxSpecInfoPageDto> SearchSandboxSpecsAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken);

    Task<SandboxSpecInfoDto> GetSandboxSpecAsync(
        string sandboxSpecId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SandboxSpecInfoDto>> BatchGetSandboxSpecsAsync(
        IReadOnlyList<string> sandboxSpecIds,
        CancellationToken cancellationToken);

    Task<SandboxSpecInfoDto> GetDefaultSandboxSpecAsync(CancellationToken cancellationToken);
}
