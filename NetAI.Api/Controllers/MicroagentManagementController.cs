using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/microagent-management")]
public class MicroagentManagementController : ControllerBase
{
    private readonly IMicroagentManagementService _microagentManagementService;

    public MicroagentManagementController(IMicroagentManagementService microagentManagementService)
    {
        _microagentManagementService = microagentManagementService;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<ConversationInfoResultSetDto>> GetConversations(
        [FromQuery(Name = "selected_repository")] string selectedRepository,
        [FromQuery(Name = "limit")] int limit = 20,
        [FromQuery(Name = "page_id")] string pageId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(selectedRepository))
        {
            return BadRequest(new { error = "selected_repository query parameter is required." });
        }

        if (limit <= 0)
        {
            return BadRequest(new { error = "limit must be greater than zero." });
        }

        ConversationInfoResultSetDto conversations = await _microagentManagementService
            .GetConversationsAsync(selectedRepository, limit, pageId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(conversations);
    }
}
