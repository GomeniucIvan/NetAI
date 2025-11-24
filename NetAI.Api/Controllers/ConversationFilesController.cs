using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}")]
public class ConversationFilesController : ControllerBase
{
    private readonly IConversationSessionService _conversationService;

    public ConversationFilesController(IConversationSessionService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet("list-files")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListFiles(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromQuery(Name = "path")] string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _conversationService.ListFilesAsync(conversationId, sessionApiKey, path, cancellationToken);
            return Ok(files);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return NotFound(new ErrorResponseDto { Error = ex.Reason });
        }
        catch (ConversationRuntimeActionException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto { Error = ex.Reason });
        }
    }

    [HttpPost("upload-files")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<FileUploadSuccessResponseDto>> UploadFiles(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromForm(Name = "files")] List<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _conversationService.UploadFilesAsync(conversationId, sessionApiKey, files, cancellationToken);
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

    [HttpGet("select-file")]
    public async Task<IActionResult> SelectFile(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromQuery(Name = "file")] string file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            FileSelectionResultDto result = await _conversationService
                .SelectFileAsync(conversationId, sessionApiKey, file, cancellationToken)
                .ConfigureAwait(false);

            return result.Status switch
            {
                FileSelectionStatus.Success => Ok(new FileContentResponseDto
                {
                    Code = result.Code ?? string.Empty
                }),
                FileSelectionStatus.Binary => StatusCode(
                    StatusCodes.Status415UnsupportedMediaType,
                    new ErrorResponseDto { Error = result.Error ?? $"Unable to open binary file: {file}" }),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ErrorResponseDto { Error = result.Error ?? $"Error opening file: {file}" })
            };
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeActionException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto { Error = ex.Reason });
        }
    }

    [HttpGet("zip-directory")]
    public async Task<IActionResult> ZipDirectory(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            WorkspaceZipStreamDto zipStream = await _conversationService
                .ZipWorkspaceAsync(conversationId, sessionApiKey, cancellationToken)
                .ConfigureAwait(false);

            return File(zipStream.Content, zipStream.ContentType, zipStream.FileName);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeActionException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto { Error = ex.Reason });
        }
    }
}
