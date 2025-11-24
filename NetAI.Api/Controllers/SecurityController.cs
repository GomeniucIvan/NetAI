using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Authentication;
using NetAI.Api.Models.Security;
using NetAI.Api.Services.Security;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/security")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.AuthenticationScheme)]
public class SecurityController : ControllerBase
{
    private readonly ISecurityService _securityService;

    public SecurityController(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    [HttpGet("policy")]
    [ProducesResponseType(typeof(SecurityPolicyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPolicy(CancellationToken cancellationToken = default)
    {
        SecurityQueryResult<SecurityPolicyResponseDto> result = await _securityService
            .GetPolicyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to load policy" });
    }

    [HttpPost("policy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePolicy([FromBody] UpdateSecurityPolicyRequestDto request, CancellationToken cancellationToken = default)
    {
        SecurityOperationResult result = await _securityService
            .UpdatePolicyAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            return Ok(new { message = result.Message ?? "Policy updated" });
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to update policy" });
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(SecurityRiskSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRiskSettings(CancellationToken cancellationToken = default)
    {
        SecurityQueryResult<SecurityRiskSettingsDto> result = await _securityService
            .GetRiskSettingsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to load risk settings" });
    }

    [HttpPost("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRiskSettings([FromBody] SecurityRiskSettingsDto request, CancellationToken cancellationToken = default)
    {
        SecurityOperationResult result = await _securityService
            .UpdateRiskSettingsAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            return Ok(new { message = result.Message ?? "Risk severity updated" });
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to update risk settings" });
    }

    [HttpGet("export-trace")]
    [ProducesResponseType(typeof(SecurityTraceExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportTrace(CancellationToken cancellationToken = default)
    {
        SecurityQueryResult<SecurityTraceExportDto> result = await _securityService
            .ExportTraceAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to export trace data" });
    }
}
