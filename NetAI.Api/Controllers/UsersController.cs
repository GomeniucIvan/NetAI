using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.User;
using NetAI.Api.Services.Git;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IGitIntegrationService _gitService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IGitIntegrationService gitService, ILogger<UsersController> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<GitUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            GitUserDto user = await _gitService
                .GetUserInfoAsync(cancellationToken)
                .ConfigureAwait(false);

            return Ok(user);
        }
        catch (GitAuthorizationException ex)
        {
            _logger.LogWarning(ex, "Unauthorized request for current user info.");
            return Unauthorized(new { error = ex.Message });
        }
    }
}
