using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Authentication;
using NetAI.Api.Models.Keys;
using NetAI.Api.Services.Keys;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/keys")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.AuthenticationScheme)]
public class KeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;

    public KeysController(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> GetApiKeys(CancellationToken cancellationToken = default)
    {
        ApiKeyQueryResult<IReadOnlyList<ApiKeyDto>> result = await _apiKeyService
            .GetApiKeysAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to fetch API keys" });
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateApiKeyResponseDto>> CreateApiKey(
        [FromBody] CreateApiKeyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        CreateApiKeyResult result = await _apiKeyService
            .CreateApiKeyAsync(request.Name, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success && result.Data is not null)
        {
            return StatusCode(StatusCodes.Status201Created, result.Data);
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to create API key" });
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteApiKey(string id, CancellationToken cancellationToken = default)
    {
        ApiKeyOperationResult result = await _apiKeyService
            .DeleteApiKeyAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            return NoContent();
        }

        return StatusCode(result.StatusCode, new { error = result.Error ?? "Failed to delete API key" });
    }
}
