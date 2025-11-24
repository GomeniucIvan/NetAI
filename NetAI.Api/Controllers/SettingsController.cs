using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Settings;
using NetAI.Api.Services.Settings;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default)
    {
        SettingsQueryResult<ApiSettingsDto> result = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            return Ok(result.Data);
        }

        if (result.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound(new { error = result.Error ?? "Settings not found" });
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Something went wrong loading settings" });
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StoreSettings([FromBody] UpdateSettingsRequestDto request, CancellationToken cancellationToken = default)
    {
        SettingsOperationResult result = await _settingsService.StoreSettingsAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            return Ok(new { message = result.Message ?? "Settings stored" });
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Something went wrong storing settings" });
    }
}
