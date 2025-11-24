using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Git;
using NetAI.Api.Models.User;
using NetAI.Api.Services.Git;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly IGitIntegrationService _gitService;
    private readonly ILogger<UserController> _logger;

    public UserController(IGitIntegrationService gitService, ILogger<UserController> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    [HttpGet("info")]
    public async Task<ActionResult<GitUserDto>> GetUserInfo(CancellationToken cancellationToken = default)
    {
        GitUserDto user = await _gitService.GetUserInfoAsync(cancellationToken);
        return Ok(user);
    }

    [HttpGet("repositories")]
    public async Task<ActionResult<IReadOnlyList<GitRepositoryDto>>> GetUserRepositories(
        [FromQuery(Name = "selected_provider")] string selectedProvider,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 30,
        [FromQuery(Name = "sort")] string sort = "pushed",
        [FromQuery(Name = "installation_id")] string installationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<GitRepositoryDto> repositories = await _gitService.GetUserRepositoriesAsync(
                selectedProvider,
                page,
                perPage,
                sort,
                installationId,
                cancellationToken);

            return Ok(repositories);
        }
        catch (GitAuthorizationException ex)
        {
            _logger.LogWarning(ex, "Unauthorized git repositories request for provider {Provider}", selectedProvider);
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid git repositories request for provider {Provider}", selectedProvider);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("search/repositories")]
    public async Task<ActionResult<IReadOnlyList<GitRepositoryDto>>> SearchRepositories(
        [FromQuery(Name = "query")] string query,
        [FromQuery(Name = "per_page")] int perPage = 100,
        [FromQuery(Name = "selected_provider")] string selectedProvider = null,
        [FromQuery(Name = "sort")] string sort = "stars",
        [FromQuery(Name = "order")] string order = "desc",
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<GitRepositoryDto> repositories = await _gitService.SearchRepositoriesAsync(
                query,
                perPage,
                selectedProvider,
                sort,
                order,
                cancellationToken);

            return Ok(repositories);
        }
        catch (GitAuthorizationException ex)
        {
            _logger.LogWarning(ex, "Unauthorized repository search for provider {Provider}", selectedProvider);
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpGet("repository/branches")]
    public async Task<ActionResult<PaginatedBranchesResponseDto>> GetRepositoryBranches(
        [FromQuery(Name = "repository")] string repository,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PaginatedBranchesResponseDto response = await _gitService.GetRepositoryBranchesAsync(
                repository,
                page,
                perPage,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid branch pagination request for repository {Repository}", repository);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("search/branches")]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> SearchBranches(
        [FromQuery(Name = "repository")] string repository,
        [FromQuery(Name = "query")] string query,
        [FromQuery(Name = "per_page")] int perPage = 30,
        [FromQuery(Name = "selected_provider")] string selectedProvider = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<BranchDto> branches = await _gitService.SearchBranchesAsync(
                repository,
                query,
                perPage,
                selectedProvider,
                cancellationToken);

            return Ok(branches);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid branch search for repository {Repository}", repository);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("installations")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetInstallations(
        [FromQuery(Name = "provider")] string provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<string> installations = await _gitService.GetInstallationIdsAsync(provider, cancellationToken);
            return Ok(installations);
        }
        catch (GitAuthorizationException ex)
        {
            _logger.LogWarning(ex, "Unauthorized installation lookup for provider {Provider}", provider);
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpGet("repository/{owner}/{repo}/microagents")]
    public async Task<ActionResult<IReadOnlyList<RepositoryMicroagentDto>>> GetRepositoryMicroagents(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<RepositoryMicroagentDto> microagents = await _gitService.GetRepositoryMicroagentsAsync(
                owner,
                repo,
                cancellationToken);

            return Ok(microagents);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid microagent listing for {Owner}/{Repository}", owner, repo);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("repository/{owner}/{repo}/microagents/content")]
    public async Task<ActionResult<MicroagentContentResponseDto>> GetRepositoryMicroagentContent(
        string owner,
        string repo,
        [FromQuery(Name = "file_path")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            MicroagentContentResponseDto response = await _gitService.GetRepositoryMicroagentContentAsync(
                owner,
                repo,
                filePath,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid microagent content request for {Owner}/{Repository}", owner, repo);
            return BadRequest(new { error = ex.Message });
        }
        catch (GitResourceNotFoundException ex)
        {
            _logger.LogInformation(ex, "Microagent not found for {Owner}/{Repository} at {Path}", owner, repo, filePath);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("suggested-tasks")]
    public async Task<ActionResult<IReadOnlyList<SuggestedTaskDto>>> GetSuggestedTasks(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SuggestedTaskDto> tasks = await _gitService.GetSuggestedTasksAsync(cancellationToken);
        return Ok(tasks);
    }
}
