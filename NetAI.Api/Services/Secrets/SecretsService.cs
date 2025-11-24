using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Secrets;

namespace NetAI.Api.Services.Secrets;

public interface ISecretsService
{
    Task<SecretsQueryResult<GetSecretsResponseDto>> GetCustomSecretsAsync(CancellationToken cancellationToken = default);

    Task<SecretsOperationResult> CreateCustomSecretAsync(CustomSecretDto secret, CancellationToken cancellationToken = default);

    Task<SecretsOperationResult> UpdateCustomSecretAsync(string secretId, CustomSecretWithoutValueDto secret, CancellationToken cancellationToken = default);

    Task<SecretsOperationResult> DeleteCustomSecretAsync(string secretId, CancellationToken cancellationToken = default);

    Task<SecretsOperationResult> StoreProviderTokensAsync(IDictionary<string, ProviderTokenDto> providerTokens, CancellationToken cancellationToken = default);

    Task<SecretsOperationResult> UnsetProviderTokensAsync(CancellationToken cancellationToken = default);

    Task<SecretsQueryResult<ProviderTokenInfo>> GetProviderTokenAsync(
        ProviderType providerType,
        CancellationToken cancellationToken = default);
}

public record class SecretsOperationResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string Message { get; init; }

    public string Error { get; init; }

    public static SecretsOperationResult SuccessResult(int statusCode, string message)
        => new()
        {
            Success = true,
            StatusCode = statusCode,
            Message = message
        };

    public static SecretsOperationResult Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public record class SecretsQueryResult<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public T Data { get; init; }

    public string Error { get; init; }

    public static SecretsQueryResult<T> SuccessResult(T data)
        => new()
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Data = data
        };

    public static SecretsQueryResult<T> Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public class SecretsService : ISecretsService
{
    private static readonly IReadOnlyDictionary<string, ProviderType> ProviderTypeMap = new Dictionary<string, ProviderType>(StringComparer.OrdinalIgnoreCase)
    {
        ["github"] = ProviderType.Github,
        ["gitlab"] = ProviderType.Gitlab,
        ["bitbucket"] = ProviderType.Bitbucket,
        ["enterprise_sso"] = ProviderType.EnterpriseSso
    };

    private readonly ISecretsStore _secretsStore;
    private readonly IProviderTokenValidator _tokenValidator;
    private readonly ILogger<SecretsService> _logger;

    public SecretsService(ISecretsStore secretsStore, IProviderTokenValidator tokenValidator, ILogger<SecretsService> logger)
    {
        _secretsStore = secretsStore;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }

    public async Task<SecretsQueryResult<GetSecretsResponseDto>> GetCustomSecretsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets secrets = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var items = new List<CustomSecretWithoutValueDto>();

            if (secrets?.CustomSecrets is not null)
            {
                foreach ((string name, CustomSecretInfo value) in secrets.CustomSecrets.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    items.Add(new CustomSecretWithoutValueDto
                    {
                        Name = name,
                        Description = value.Description
                    });
                }
            }

            return SecretsQueryResult<GetSecretsResponseDto>.SuccessResult(new GetSecretsResponseDto
            {
                CustomSecrets = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load secret names");
            return SecretsQueryResult<GetSecretsResponseDto>.Failure(StatusCodes.Status401Unauthorized, "Failed to get secret names");
        }
    }

    public async Task<SecretsOperationResult> CreateCustomSecretAsync(CustomSecretDto secret, CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets existing = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            UserSecrets working = existing?.Clone() ?? UserSecrets.Empty.Clone();

            if (working.CustomSecrets.ContainsKey(secret.Name))
            {
                return SecretsOperationResult.Failure(StatusCodes.Status400BadRequest, $"Secret {secret.Name} already exists");
            }

            string description = secret.Description ?? string.Empty;
            working.CustomSecrets[secret.Name] = new CustomSecretInfo(secret.Value, description);

            var updated = new UserSecrets(working.ProviderTokens, working.CustomSecrets);
            await _secretsStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);

            return SecretsOperationResult.SuccessResult(StatusCodes.Status201Created, "Secret created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Something went wrong creating secret");
            return SecretsOperationResult.Failure(StatusCodes.Status500InternalServerError, "Something went wrong creating secret");
        }
    }

    public async Task<SecretsOperationResult> UpdateCustomSecretAsync(string secretId, CustomSecretWithoutValueDto secret, CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets existing = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return SecretsOperationResult.SuccessResult(StatusCodes.Status200OK, "Secret updated successfully");
            }

            if (!existing.CustomSecrets.TryGetValue(secretId, out CustomSecretInfo existingSecret))
            {
                return SecretsOperationResult.Failure(StatusCodes.Status404NotFound, $"Secret with ID {secretId} not found");
            }

            string newName = secret.Name;
            string description = secret.Description ?? string.Empty;

            var customSecrets = new Dictionary<string, CustomSecretInfo>(existing.CustomSecrets, StringComparer.OrdinalIgnoreCase);
            customSecrets.Remove(secretId);

            if (!string.Equals(newName, secretId, StringComparison.OrdinalIgnoreCase)
                && customSecrets.ContainsKey(newName))
            {
                return SecretsOperationResult.Failure(StatusCodes.Status400BadRequest, $"Secret {newName} already exists");
            }

            customSecrets[newName] = existingSecret with { Description = description };

            var updated = new UserSecrets(existing.ProviderTokens, customSecrets);
            await _secretsStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);

            return SecretsOperationResult.SuccessResult(StatusCodes.Status200OK, "Secret updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Something went wrong updating secret");
            return SecretsOperationResult.Failure(StatusCodes.Status500InternalServerError, "Something went wrong updating secret");
        }
    }

    public async Task<SecretsOperationResult> DeleteCustomSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets existing = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return SecretsOperationResult.SuccessResult(StatusCodes.Status200OK, "Secret deleted successfully");
            }

            var customSecrets = new Dictionary<string, CustomSecretInfo>(existing.CustomSecrets, StringComparer.OrdinalIgnoreCase);
            if (!customSecrets.Remove(secretId))
            {
                return SecretsOperationResult.Failure(StatusCodes.Status404NotFound, $"Secret with ID {secretId} not found");
            }

            var updated = new UserSecrets(existing.ProviderTokens, customSecrets);
            await _secretsStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);

            return SecretsOperationResult.SuccessResult(StatusCodes.Status200OK, "Secret deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Something went wrong deleting secret");
            return SecretsOperationResult.Failure(StatusCodes.Status500InternalServerError, "Something went wrong deleting secret");
        }
    }

    public async Task<SecretsOperationResult> StoreProviderTokensAsync(IDictionary<string, ProviderTokenDto> providerTokens, CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets loaded = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            UserSecrets existing = loaded ?? UserSecrets.Empty.Clone();

            string validationError = await ValidateProviderTokensAsync(providerTokens, existing, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(validationError))
            {
                _logger.LogInformation("Provider token validation failed: {Error}", validationError);
                return SecretsOperationResult.Failure(StatusCodes.Status401Unauthorized, validationError);
            }

            IDictionary<ProviderType, ProviderTokenInfo> merged = MergeProviderTokens(providerTokens, existing);
            var updated = new UserSecrets(merged, existing.CustomSecrets);
            await _secretsStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);

            return SecretsOperationResult.SuccessResult(StatusCodes.Status200OK, "Git providers stored");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Something went wrong storing git providers");
            return SecretsOperationResult.Failure(StatusCodes.Status500InternalServerError, "Something went wrong storing git providers");
        }
    }

    public async Task<SecretsOperationResult> UnsetProviderTokensAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets existing = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                await _secretsStore.StoreAsync(new UserSecrets(new Dictionary<ProviderType, ProviderTokenInfo>(), new Dictionary<string, CustomSecretInfo>(StringComparer.OrdinalIgnoreCase)), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var updated = new UserSecrets(new Dictionary<ProviderType, ProviderTokenInfo>(), existing.CustomSecrets);
                await _secretsStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);
            }

            return SecretsOperationResult.SuccessResult(StatusCodes.Status200OK, "Unset Git provider tokens");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Something went wrong unsetting provider tokens");
            return SecretsOperationResult.Failure(StatusCodes.Status500InternalServerError, "Something went wrong unsetting tokens");
        }
    }

    public async Task<SecretsQueryResult<ProviderTokenInfo>> GetProviderTokenAsync(
        ProviderType providerType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            UserSecrets secrets = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);

            if (secrets is null || !secrets.ProviderTokens.TryGetValue(providerType, out ProviderTokenInfo token) || string.IsNullOrWhiteSpace(token.Token))
            {
                return SecretsQueryResult<ProviderTokenInfo>.Failure(
                    StatusCodes.Status404NotFound,
                    "Provider token not found");
            }

            return SecretsQueryResult<ProviderTokenInfo>.SuccessResult(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve provider token for {Provider}", providerType);
            return SecretsQueryResult<ProviderTokenInfo>.Failure(
                StatusCodes.Status500InternalServerError,
                "Failed to retrieve provider token");
        }
    }

    private async Task<string> ValidateProviderTokensAsync(IDictionary<string, ProviderTokenDto> incoming, UserSecrets existing, CancellationToken cancellationToken)
    {
        if (incoming is null || incoming.Count == 0)
        {
            return null;
        }

        foreach ((string providerKey, ProviderTokenDto tokenPayload) in incoming)
        {
            if (!TryParseProvider(providerKey, out ProviderType providerType))
            {
                continue;
            }

            string tokenValue = Normalize(tokenPayload?.Token);
            string hostValue = Normalize(tokenPayload?.Host);

            if (!string.IsNullOrEmpty(tokenValue))
            {
                ProviderType? confirmedType = await _tokenValidator.ValidateAsync(tokenValue, hostValue, cancellationToken).ConfigureAwait(false);
                string message = ProcessTokenValidationResult(confirmedType, providerType);
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }

            if (existing.ProviderTokens.TryGetValue(providerType, out ProviderTokenInfo existingToken)
                && !string.Equals(Normalize(existingToken.Host), hostValue, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(existingToken.Token))
            {
                ProviderType? confirmedType = await _tokenValidator.ValidateAsync(existingToken.Token!, hostValue, cancellationToken).ConfigureAwait(false);
                string message = ProcessTokenValidationResult(confirmedType, providerType);
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }
        }

        return null;
    }

    private static IDictionary<ProviderType, ProviderTokenInfo> MergeProviderTokens(IDictionary<string, ProviderTokenDto> incoming, UserSecrets existing)
    {
        if (incoming is null || incoming.Count == 0)
        {
            return new Dictionary<ProviderType, ProviderTokenInfo>();
        }

        var merged = new Dictionary<ProviderType, ProviderTokenInfo>();

        foreach ((string providerKey, ProviderTokenDto tokenPayload) in incoming)
        {
            if (!TryParseProvider(providerKey, out ProviderType providerType))
            {
                continue;
            }

            string tokenValue = Normalize(tokenPayload?.Token);
            string hostValue = Normalize(tokenPayload?.Host);

            ProviderTokenInfo valueToStore;
            if (string.IsNullOrEmpty(tokenValue)
                && existing.ProviderTokens.TryGetValue(providerType, out ProviderTokenInfo existingToken)
                && !string.IsNullOrEmpty(existingToken.Token))
            {
                valueToStore = existingToken with { Host = hostValue };
            }
            else
            {
                valueToStore = new ProviderTokenInfo(tokenValue, hostValue);
            }

            merged[providerType] = valueToStore;
        }

        return merged;
    }

    private static bool TryParseProvider(string providerKey, out ProviderType providerType)
    {
        if (ProviderTypeMap.TryGetValue(providerKey, out providerType))
        {
            return true;
        }

        providerType = default;
        return false;
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ProcessTokenValidationResult(ProviderType? confirmedType, ProviderType expectedType)
    {
        if (!confirmedType.HasValue || confirmedType.Value != expectedType)
        {
            string providerName = ToProviderName(expectedType);
            return $"Invalid token. Please make sure it is a valid {providerName} token.";
        }

        return null;
    }

    private static string ToProviderName(ProviderType providerType)
        => providerType switch
        {
            ProviderType.EnterpriseSso => "enterprise_sso",
            ProviderType.Github => "github",
            ProviderType.Gitlab => "gitlab",
            ProviderType.Bitbucket => "bitbucket",
            _ => providerType.ToString().ToLowerInvariant()
        };
}
