using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Keys;

namespace NetAI.Api.Services.Keys;

public interface IApiKeyService
{
    Task<ApiKeyQueryResult<IReadOnlyList<ApiKeyDto>>> GetApiKeysAsync(CancellationToken cancellationToken = default);

    Task<CreateApiKeyResult> CreateApiKeyAsync(string name, CancellationToken cancellationToken = default);

    Task<ApiKeyOperationResult> DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default);

    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string presentedKey, CancellationToken cancellationToken = default);
}

public record class ApiKeyOperationResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string Error { get; init; }

    public static ApiKeyOperationResult SuccessResult(int statusCode)
        => new()
        {
            Success = true,
            StatusCode = statusCode
        };

    public static ApiKeyOperationResult Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public record class ApiKeyQueryResult<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public T Data { get; init; }

    public string Error { get; init; }

    public static ApiKeyQueryResult<T> SuccessResult(T data)
        => new()
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Data = data
        };

    public static ApiKeyQueryResult<T> Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public record class CreateApiKeyResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public CreateApiKeyResponseDto Data { get; init; }

    public string Error { get; init; }

    public static CreateApiKeyResult SuccessResult(CreateApiKeyResponseDto response)
        => new()
        {
            Success = true,
            StatusCode = StatusCodes.Status201Created,
            Data = response
        };

    public static CreateApiKeyResult Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public record class ApiKeyValidationResult
{
    public bool Success { get; init; }

    public ApiKeyRecord ApiKey { get; init; }

    public static ApiKeyValidationResult Valid(ApiKeyRecord record)
        => new()
        {
            Success = true,
            ApiKey = record.Copy()
        };

    public static ApiKeyValidationResult Invalid()
        => new()
        {
            Success = false
        };
}

public class ApiKeyService : IApiKeyService
{
    private const int RawKeyByteLength = 32;
    private const int PrefixLength = 8;
    private const string KeyPrefix = "nak_";

    private readonly IApiKeyStore _store;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(IApiKeyStore store, ILogger<ApiKeyService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<ApiKeyQueryResult<IReadOnlyList<ApiKeyDto>>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyCollection<ApiKeyRecord> records = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

            IReadOnlyList<ApiKeyDto> data = records
                .OrderByDescending(static record => record.CreatedAt)
                .Select(static record => new ApiKeyDto
                {
                    Id = record.Id.ToString(),
                    Name = record.Name,
                    Prefix = record.Prefix,
                    CreatedAt = record.CreatedAt,
                    LastUsedAt = record.LastUsedAt
                })
                .ToList();

            return ApiKeyQueryResult<IReadOnlyList<ApiKeyDto>>.SuccessResult(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load API keys");
            return ApiKeyQueryResult<IReadOnlyList<ApiKeyDto>>.Failure(
                StatusCodes.Status500InternalServerError,
                "Failed to load API keys");
        }
    }

    public async Task<CreateApiKeyResult> CreateApiKeyAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CreateApiKeyResult.Failure(StatusCodes.Status400BadRequest, "Name is required");
        }

        try
        {
            string trimmedName = name.Trim();
            string rawKey = GenerateRawKey();
            string fullKey = FormatKey(rawKey);
            string hashedKey = HashKey(fullKey);
            string prefix = BuildPrefix(rawKey);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var record = new ApiKeyRecord
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                Prefix = prefix,
                HashedKey = hashedKey,
                CreatedAt = now,
                LastUsedAt = null
            };

            await _store.AddAsync(record, cancellationToken).ConfigureAwait(false);

            var response = new CreateApiKeyResponseDto
            {
                Id = record.Id.ToString(),
                Name = record.Name,
                Key = fullKey,
                Prefix = record.Prefix,
                CreatedAt = record.CreatedAt
            };

            return CreateApiKeyResult.SuccessResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create API key");
            return CreateApiKeyResult.Failure(
                StatusCodes.Status500InternalServerError,
                "Failed to create API key");
        }
    }

    public async Task<ApiKeyOperationResult> DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return ApiKeyOperationResult.Failure(StatusCodes.Status400BadRequest, "API key id is required");
        }

        if (!Guid.TryParse(id, out Guid keyId))
        {
            return ApiKeyOperationResult.Failure(StatusCodes.Status400BadRequest, "Invalid API key id");
        }

        try
        {
            bool removed = await _store.TryRemoveAsync(keyId, cancellationToken).ConfigureAwait(false);
            if (!removed)
            {
                return ApiKeyOperationResult.Failure(StatusCodes.Status404NotFound, "API key not found");
            }

            return ApiKeyOperationResult.SuccessResult(StatusCodes.Status204NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete API key {ApiKeyId}", id);
            return ApiKeyOperationResult.Failure(
                StatusCodes.Status500InternalServerError,
                "Failed to delete API key");
        }
    }

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string presentedKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return ApiKeyValidationResult.Invalid();
        }

        try
        {
            string hashed = HashKey(presentedKey.Trim());
            ApiKeyRecord record = await _store.TryGetByHashAsync(hashed, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return ApiKeyValidationResult.Invalid();
            }

            var updated = record with { LastUsedAt = DateTimeOffset.UtcNow };
            bool success = await _store.TryUpdateAsync(updated, cancellationToken).ConfigureAwait(false);
            if (!success)
            {
                _logger.LogWarning("Failed to persist last used timestamp for API key {ApiKeyId}", record.Id);
            }

            return ApiKeyValidationResult.Valid(updated);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate API key");
            return ApiKeyValidationResult.Invalid();
        }
    }

    private static string GenerateRawKey()
    {
        Span<byte> buffer = stackalloc byte[RawKeyByteLength];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    private static string FormatKey(string rawKey)
        => string.Create(KeyPrefix.Length + rawKey.Length, rawKey, static (span, state) =>
        {
            KeyPrefix.AsSpan().CopyTo(span);
            state.AsSpan().CopyTo(span[KeyPrefix.Length..]);
        });

    private static string BuildPrefix(string rawKey)
    {
        int length = Math.Min(PrefixLength, rawKey.Length);
        string prefix = rawKey[..length];
        return string.Create(KeyPrefix.Length + length, prefix, static (span, state) =>
        {
            KeyPrefix.AsSpan().CopyTo(span);
            state.AsSpan().CopyTo(span[KeyPrefix.Length..]);
        });
    }

    private static string HashKey(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] hashBytes = SHA256.HashData(keyBytes);
        return Convert.ToHexString(hashBytes);
    }
}
