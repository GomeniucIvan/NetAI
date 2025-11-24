using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Installation;
using NetAI.Api.Services.Installation;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/install")]
public class InstallController : ControllerBase
{
    private readonly IInstallationService _installationService;

    public InstallController(IInstallationService installationService)
    {
        _installationService = installationService;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(InstallStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InstallStatusDto>> GetStatus(CancellationToken cancellationToken = default)
    {
        InstallStatusDto status = await _installationService.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Install([FromBody] InstallRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        InstallationResult result = await _installationService.InstallAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            return Ok(new { message = result.Message ?? "Installation completed successfully." });
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Installation failed." });
    }
}
