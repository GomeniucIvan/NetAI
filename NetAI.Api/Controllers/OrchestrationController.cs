using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Authentication;
using NetAI.Api.Models;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Services.Sandboxes;

namespace NetAI.Api.Controllers;

//Todo sln

[ApiController]
[Route("api/v1/orchestration/sandboxes")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.AuthenticationScheme)]
public class OrchestrationController : ControllerBase
{
    private readonly ISandboxOrchestrationClient _sandboxOrchestrationClient;
    private readonly ISandboxSpecService _sandboxSpecService;

    public OrchestrationController(
        ISandboxOrchestrationClient sandboxOrchestrationClient,
        ISandboxSpecService sandboxSpecService)
    {
        _sandboxOrchestrationClient = sandboxOrchestrationClient;
        _sandboxSpecService = sandboxSpecService;
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(SandboxOrchestrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SandboxOrchestrationSessionDto>> StartSandboxAsync(
        [FromBody] SandboxOrchestrationStartRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(CreateError("A request body is required."));
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        SandboxSpecInfoDto spec = request.SandboxSpec;
        if (spec is null)
        {
            if (string.IsNullOrWhiteSpace(request.SandboxSpecId))
            {
                return BadRequest(CreateError("A sandbox_spec_id or sandbox_spec must be provided."));
            }

            spec = await _sandboxSpecService
                .GetSandboxSpecAsync(request.SandboxSpecId, cancellationToken)
                .ConfigureAwait(false);

            if (spec is null)
            {
                return NotFound(CreateError($"Sandbox spec '{request.SandboxSpecId}' was not found."));
            }
        }

        string sandboxId = string.IsNullOrWhiteSpace(request.SandboxId)
            ? Guid.NewGuid().ToString("N")
            : request.SandboxId;

        try
        {
            SandboxProvisioningResult result = await _sandboxOrchestrationClient
                .StartSandboxAsync(sandboxId, spec, cancellationToken)
                .ConfigureAwait(false);

            SandboxOrchestrationSessionDto dto = MapResult(sandboxId, spec.Id, result);
            return Ok(dto);
        }
        catch (SandboxOrchestrationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, CreateError(ex.Message));
        }
    }

    [HttpPost("{sandboxId}/resume")]
    [ProducesResponseType(typeof(SandboxOrchestrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SandboxOrchestrationSessionDto>> ResumeSandboxAsync(
        string sandboxId,
        [FromBody] SandboxOrchestrationResumeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SandboxProvisioningResult result = await _sandboxOrchestrationClient
                .ResumeSandboxAsync(sandboxId, request?.RuntimeId, cancellationToken)
                .ConfigureAwait(false);

            SandboxOrchestrationSessionDto dto = MapResult(sandboxId, null, result);
            return Ok(dto);
        }
        catch (SandboxOrchestrationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, CreateError(ex.Message));
        }
    }

    [HttpPost("{sandboxId}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PauseSandboxAsync(
        string sandboxId,
        [FromBody] SandboxOrchestrationPauseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool paused = await _sandboxOrchestrationClient
                .PauseSandboxAsync(sandboxId, request?.RuntimeId, cancellationToken)
                .ConfigureAwait(false);

            if (!paused)
            {
                return NotFound(CreateError($"Sandbox '{sandboxId}' runtime could not be resolved."));
            }

            return NoContent();
        }
        catch (SandboxOrchestrationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, CreateError(ex.Message));
        }
    }

    private static SandboxOrchestrationSessionDto MapResult(
        string sandboxId,
        string sandboxSpecId,
        SandboxProvisioningResult result)
    {
        return new SandboxOrchestrationSessionDto
        {
            SandboxId = sandboxId,
            SandboxSpecId = sandboxSpecId,
            Status = result.Status,
            SessionApiKey = result.SessionApiKey,
            RuntimeId = result.RuntimeId,
            RuntimeUrl = result.RuntimeUrl,
            WorkspacePath = result.WorkspacePath,
            ExposedUrls = result.ExposedUrls,
            RuntimeHosts = result.RuntimeHosts,
            RuntimeStateJson = result.RuntimeStateJson,
        };
    }

    private static ErrorResponseDto CreateError(string message)
        => new() { Error = message };
}
