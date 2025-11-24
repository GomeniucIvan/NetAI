using Microsoft.AspNetCore.Mvc;

namespace NetAI.RuntimeGateway.Controllers;

[ApiController]
[Route("runtime")]
public class RuntimeController : ControllerBase
{
    [HttpPost("start")]
    public IActionResult StartRuntime()
    {
        return Ok(new
        {
            runtime_id = Guid.NewGuid().ToString(),
            url = "wss://localhost:7251/runtime/mock/sockets/events",
            session_api_key = "mock-session-key",
            working_dir = "/workspace",
            status = "running",
            sandbox_spec_id = "default"
        });
    }

    [HttpGet("conversations/{id}")]
    public IActionResult GetConversation(string id)
    {
        return Ok(new
        {
            id,
            status = "ready"
        });
    }
}
