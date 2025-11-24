using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Diagnostics;
using NetAI.Api.Services.Diagnostics;

namespace NetAI.Api.Controllers;

//TODO remove

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private static readonly object AlivePayload = new { status = "ok" };
    private readonly ISystemInfoProvider _systemInfoProvider;

    public HealthController(ISystemInfoProvider systemInfoProvider)
    {
        _systemInfoProvider = systemInfoProvider;
    }

    [HttpGet("alive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAlive()
    {
        return Ok(AlivePayload);
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok("OK");
    }

    [HttpGet("server_info")]
    [ProducesResponseType(typeof(SystemInfoDto), StatusCodes.Status200OK)]
    public ActionResult<SystemInfoDto> GetServerInfo()
    {
        SystemInfoDto info = _systemInfoProvider.GetSystemInfo();
        return Ok(info);
    }
}
