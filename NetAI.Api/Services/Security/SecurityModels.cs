using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace NetAI.Api.Services.Security;

public record class SecurityStateRecord
{
    public const int DefaultRiskSeverity = 2;

    private const string DefaultPolicyText = """
# Default Invariant Policy
# Allow all actions by default. Update this policy to enforce project-specific guardrails.
allow {
    true
}
""";

    [JsonPropertyName("policy")]
    public string Policy { get; init; } = DefaultPolicyText;

    [JsonPropertyName("risk_severity")]
    public int RiskSeverity { get; init; } = DefaultRiskSeverity;

    [JsonPropertyName("policy_updated_at")]
    public DateTimeOffset? PolicyUpdatedAt { get; init; }

    [JsonPropertyName("risk_severity_updated_at")]
    public DateTimeOffset? RiskSeverityUpdatedAt { get; init; }

    public static SecurityStateRecord CreateDefault()
        => new();

    public SecurityStateRecord Copy()
        => this with { };
}

public record class SecurityOperationResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string Message { get; init; }

    public string Error { get; init; }

    public static SecurityOperationResult SuccessResult(int statusCode, string message)
        => new()
        {
            Success = true,
            StatusCode = statusCode,
            Message = message
        };

    public static SecurityOperationResult Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}

public record class SecurityQueryResult<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public T Data { get; init; }

    public string Error { get; init; }

    public static SecurityQueryResult<T> SuccessResult(T data)
        => new()
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Data = data
        };

    public static SecurityQueryResult<T> Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error
        };
}
