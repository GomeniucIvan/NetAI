using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Services.Sandboxes;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/v1/sandboxes")]
public class SandboxesController : ControllerBase
{
    private readonly ISandboxService _sandboxService;

    public SandboxesController(ISandboxService sandboxService)
    {
        _sandboxService = sandboxService;
    }

    [HttpGet]
    public async Task<ActionResult<SandboxPageDto>> SearchAsync(
        [FromQuery(Name = "page_id")] string pageId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        SandboxPageDto page = await _sandboxService
            .SearchSandboxesAsync(pageId, limit, cancellationToken)
            .ConfigureAwait(false);
        return Ok(page);
    }

    [HttpGet("{sandboxId}")]
    public async Task<ActionResult<SandboxInfoDto>> GetAsync(
        string sandboxId,
        CancellationToken cancellationToken = default)
    {
        SandboxInfoDto sandbox = await _sandboxService
            .GetSandboxAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        if (sandbox is null)
        {
            return NotFound();
        }

        return Ok(sandbox);
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<SandboxInfoDto>>> BatchGetAsync(
        [FromBody] BatchGetSandboxesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        IReadOnlyList<SandboxInfoDto> sandboxes = await _sandboxService
            .BatchGetSandboxesAsync(request.SandboxIds.ToList(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(sandboxes);
    }

    [HttpPost]
    public async Task<ActionResult<SandboxInfoDto>> StartAsync(
        [FromBody] StartSandboxRequestDto request,
        CancellationToken cancellationToken = default)
    {
        SandboxInfoDto sandbox = await _sandboxService
            .StartSandboxAsync(request, cancellationToken)
            .ConfigureAwait(false);

        //vs error
        return CreatedAtAction(nameof(GetAsync), new { sandboxId = sandbox.Id }, sandbox);
    }

    [HttpPost("{sandboxId}/pause")]
    public async Task<IActionResult> PauseAsync(
        string sandboxId,
        CancellationToken cancellationToken = default)
    {
        bool success = await _sandboxService
            .PauseSandboxAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        return success ? NoContent() : NotFound();
    }

    [HttpPost("{sandboxId}/resume")]
    public async Task<IActionResult> ResumeAsync(
        string sandboxId,
        CancellationToken cancellationToken = default)
    {
        bool success = await _sandboxService
            .ResumeSandboxAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{sandboxId}")]
    public async Task<IActionResult> DeleteAsync(
        string sandboxId,
        CancellationToken cancellationToken = default)
    {
        bool success = await _sandboxService
            .DeleteSandboxAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        return success ? NoContent() : NotFound();
    }
}
