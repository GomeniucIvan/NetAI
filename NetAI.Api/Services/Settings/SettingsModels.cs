using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using NetAI.Api.Models.Settings;

namespace NetAI.Api.Services.Settings;

public record class StoredSettings
{
    public string Language { get; init; }

    public string Agent { get; init; }

    public int? MaxIterations { get; init; }

    public string SecurityAnalyzer { get; init; }

    public bool? ConfirmationMode { get; init; }

    public string LlmModel { get; init; }

    public string LlmApiKey { get; init; }

    public string LlmBaseUrl { get; init; }

    public double? RemoteRuntimeResourceFactor { get; init; }

    public bool EnableDefaultCondenser { get; init; } = true;

    public int? CondenserMaxSize { get; init; }

    public bool EnableSoundNotifications { get; init; }

    public bool EnableProactiveConversationStarters { get; init; } = true;

    public bool EnableSolvabilityAnalysis { get; init; } = true;

    public bool? UserConsentsToAnalytics { get; init; }

    public string SandboxBaseContainerImage { get; init; }

    public string SandboxRuntimeContainerImage { get; init; }

    public McpConfigDto McpConfig { get; init; }

    public string SearchApiKey { get; init; }

    public string SandboxApiKey { get; init; }

    public double? MaxBudgetPerTask { get; init; }

    public string Email { get; init; }

    public bool? EmailVerified { get; init; }

    public string GitUserName { get; init; }

    public string GitUserEmail { get; init; }

    public LegacySecretsStore SecretsStore { get; init; }

    public StoredSettings Copy()
        => this with
        {
            McpConfig = McpConfig?.Clone(),
            SecretsStore = SecretsStore?.Copy()
        };
}

public record class LegacySecretsStore
{
    public IDictionary<string, LegacyProviderToken> ProviderTokens { get; init; }

    public IDictionary<string, LegacyCustomSecret> CustomSecrets { get; init; }

    public LegacySecretsStore Copy()
        => this with
        {
            ProviderTokens = ProviderTokens is not null
                ? new Dictionary<string, LegacyProviderToken>(ProviderTokens, StringComparer.OrdinalIgnoreCase)
                : null,
            CustomSecrets = CustomSecrets is not null
                ? new Dictionary<string, LegacyCustomSecret>(CustomSecrets, StringComparer.OrdinalIgnoreCase)
                : null
        };
}

public record class LegacyProviderToken
{
    public string Token { get; init; }

    public string UserId { get; init; }

    public string Host { get; init; }
}

public record class LegacyCustomSecret
{
    public string Secret { get; init; }

    public string Description { get; init; }
}

public record class SettingsOperationResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string Message { get; init; }

    public string Error { get; init; }

    public static SettingsOperationResult SuccessResult(int statusCode, string message)
        => new()
        {
            Success = true,
            StatusCode = statusCode,
            Message = message
        };

    public static SettingsOperationResult Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public record class SettingsQueryResult<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public T Data { get; init; }

    public string Error { get; init; }

    public static SettingsQueryResult<T> SuccessResult(T data)
        => new()
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Data = data
        };

    public static SettingsQueryResult<T> Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}
