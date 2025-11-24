using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Configuration;
using NetAI.Api.Services.Configuration;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/options")]
public class OptionsController : ControllerBase
{
    private readonly IOptionsMetadataService _optionsService;

    public OptionsController(IOptionsMetadataService optionsService)
    {
        _optionsService = optionsService;
    }

    [HttpGet("models")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetModels()
    {
        IReadOnlyList<string> models = _optionsService.GetModels();
        return Ok(models);
    }

    [HttpGet("agents")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetAgents()
    {
        IReadOnlyList<string> agents = _optionsService.GetAgents();
        return Ok(agents);
    }

    [HttpGet("security-analyzers")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetSecurityAnalyzers()
    {
        IReadOnlyList<string> analyzers = _optionsService.GetSecurityAnalyzers();
        return Ok(analyzers);
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(GetConfigResponseDto), StatusCodes.Status200OK)]
    public ActionResult<GetConfigResponseDto> GetConfig()
    {
        GetConfigResponseDto config = _optionsService.GetConfig();
        return Ok(config);
    }
}
