using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Sandboxes;
using NetAI.Api.Services.Sandboxes;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/v1/sandbox-specs")]
public class SandboxSpecsController : ControllerBase
{
    private readonly ISandboxSpecService _sandboxSpecService;

    public SandboxSpecsController(ISandboxSpecService sandboxSpecService)
    {
        _sandboxSpecService = sandboxSpecService;
    }

    [HttpGet]
    public async Task<ActionResult<SandboxSpecInfoPageDto>> SearchAsync(
        [FromQuery(Name = "page_id")] string pageId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        SandboxSpecInfoPageDto page = await _sandboxSpecService
            .SearchSandboxSpecsAsync(pageId, limit, cancellationToken)
            .ConfigureAwait(false);
        return Ok(page);
    }

    [HttpGet("{sandboxSpecId}")]
    public async Task<ActionResult<SandboxSpecInfoDto>> GetAsync(
        string sandboxSpecId,
        CancellationToken cancellationToken = default)
    {
        SandboxSpecInfoDto spec = await _sandboxSpecService
            .GetSandboxSpecAsync(sandboxSpecId, cancellationToken)
            .ConfigureAwait(false);

        if (spec is null)
        {
            return NotFound();
        }

        return Ok(spec);
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<SandboxSpecInfoDto>>> BatchGetAsync(
        [FromBody] BatchGetSandboxSpecsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        IReadOnlyList<SandboxSpecInfoDto> specs = await _sandboxSpecService
            .BatchGetSandboxSpecsAsync(request.SandboxSpecIds.ToList(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(specs);
    }
}
