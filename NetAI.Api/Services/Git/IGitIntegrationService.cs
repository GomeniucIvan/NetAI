using NetAI.Api.Models.Git;
using NetAI.Api.Models.User;

namespace NetAI.Api.Services.Git;

public interface IGitIntegrationService
{
    Task<GitUserDto> GetUserInfoAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<GitRepositoryDto>> GetUserRepositoriesAsync(
        string selectedProvider,
        int page,
        int perPage,
        string sort,
        string installationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitRepositoryDto>> SearchRepositoriesAsync(
        string query,
        int perPage,
        string selectedProvider,
        string sort,
        string order,
        CancellationToken cancellationToken);

    Task<PaginatedBranchesResponseDto> GetRepositoryBranchesAsync(
        string repository,
        int page,
        int perPage,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchDto>> SearchBranchesAsync(
        string repository,
        string query,
        int perPage,
        string selectedProvider,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetInstallationIdsAsync(
        string provider,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RepositoryMicroagentDto>> GetRepositoryMicroagentsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken);

    Task<MicroagentContentResponseDto> GetRepositoryMicroagentContentAsync(
        string owner,
        string repository,
        string filePath,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SuggestedTaskDto>> GetSuggestedTasksAsync(CancellationToken cancellationToken);
}
