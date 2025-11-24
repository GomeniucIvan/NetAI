using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}")]
public class ConversationFeedbackController : ControllerBase
{
    private readonly IConversationSessionService _conversationService;

    public ConversationFeedbackController(IConversationSessionService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpPost("submit-feedback")]
    public async Task<ActionResult<FeedbackResponseDto>> SubmitFeedback(
        string conversationId,
        [FromBody] FeedbackDto feedback,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _conversationService.SubmitFeedbackAsync(conversationId, sessionApiKey, feedback, cancellationToken);
            return Ok(response);
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
}
