using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models.Secrets;
using NetAI.Api.Services.Secrets;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api")]
public class SecretsController : ControllerBase
{
    private readonly ISecretsService _secretsService;

    public SecretsController(ISecretsService secretsService)
    {
        _secretsService = secretsService;
    }

    [HttpGet("secrets")]
    public async Task<ActionResult<GetSecretsResponseDto>> GetSecrets(CancellationToken cancellationToken = default)
    {
        SecretsQueryResult<GetSecretsResponseDto> result = await _secretsService
            .GetCustomSecretsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return CreateErrorResult<GetSecretsResponseDto>(result.StatusCode, result.Error);
        }

        return Ok(result.Data);
    }

    [HttpPost("secrets")]
    public async Task<ActionResult> CreateSecret([FromBody] CustomSecretDto secret, CancellationToken cancellationToken = default)
    {
        SecretsOperationResult result = await _secretsService
            .CreateCustomSecretAsync(secret, cancellationToken)
            .ConfigureAwait(false);

        return CreateOperationResult(result);
    }

    [HttpPut("secrets/{secretId}")]
    public async Task<ActionResult> UpdateSecret(
        string secretId,
        [FromBody] CustomSecretWithoutValueDto secret,
        CancellationToken cancellationToken = default)
    {
        SecretsOperationResult result = await _secretsService
            .UpdateCustomSecretAsync(secretId, secret, cancellationToken)
            .ConfigureAwait(false);

        return CreateOperationResult(result);
    }

    [HttpDelete("secrets/{secretId}")]
    public async Task<ActionResult> DeleteSecret(string secretId, CancellationToken cancellationToken = default)
    {
        SecretsOperationResult result = await _secretsService
            .DeleteCustomSecretAsync(secretId, cancellationToken)
            .ConfigureAwait(false);

        return CreateOperationResult(result);
    }

    [HttpPost("add-git-providers")]
    public async Task<ActionResult> AddGitProviders(
        [FromBody] ProviderTokensRequestDto request,
        CancellationToken cancellationToken = default)
    {
        SecretsOperationResult result = await _secretsService
            .StoreProviderTokensAsync(request.ProviderTokens, cancellationToken)
            .ConfigureAwait(false);

        return CreateOperationResult(result);
    }

    [HttpPost("unset-provider-tokens")]
    public async Task<ActionResult> UnsetProviderTokens(CancellationToken cancellationToken = default)
    {
        SecretsOperationResult result = await _secretsService
            .UnsetProviderTokensAsync(cancellationToken)
            .ConfigureAwait(false);

        return CreateOperationResult(result);
    }

    private ActionResult CreateOperationResult(SecretsOperationResult result)
    {
        if (result.Success)
        {
            object payload = new { message = result.Message ?? string.Empty };
            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Ok(payload),
                StatusCodes.Status201Created => StatusCode(StatusCodes.Status201Created, payload),
                _ => StatusCode(result.StatusCode, payload)
            };
        }

        return CreateErrorResult(result.StatusCode, result.Error);
    }

    private ActionResult<T> CreateErrorResult<T>(int statusCode, string error)
    {
        object payload = new { error = error ?? string.Empty };
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => BadRequest(payload),
            StatusCodes.Status401Unauthorized => Unauthorized(payload),
            StatusCodes.Status404NotFound => NotFound(payload),
            _ => StatusCode(statusCode, payload)
        };
    }

    private ActionResult CreateErrorResult(int statusCode, string error)
    {
        object payload = new { error = error ?? string.Empty };
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => BadRequest(payload),
            StatusCodes.Status401Unauthorized => Unauthorized(payload),
            StatusCodes.Status404NotFound => NotFound(payload),
            _ => StatusCode(statusCode, payload)
        };
    }
}
