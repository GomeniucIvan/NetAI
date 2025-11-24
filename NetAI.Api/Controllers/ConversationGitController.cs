using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Git;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}/git")]
public class ConversationGitController : ControllerBase
{
    private readonly IConversationSessionService _conversationService;

    public ConversationGitController(IConversationSessionService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet("changes")]
    public async Task<ActionResult<IReadOnlyList<GitChangeDto>>> GetChanges(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var changes = await _conversationService.GetGitChangesAsync(conversationId, sessionApiKey, cancellationToken);
            return Ok(changes);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("diff")]
    public async Task<ActionResult<GitChangeDiffDto>> GetDiff(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromQuery(Name = "path")] string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var diff = await _conversationService.GetGitDiffAsync(conversationId, sessionApiKey, path, cancellationToken);
            return Ok(diff);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationResourceNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
