using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class BuildStatusResponse
{
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "success",
        "failed",
        "failure",
        "cancelled",
        "canceled",
        "timeout",
        "internal_error",
        "expired"
    };

    [JsonPropertyName("build_id")]
    public string BuildId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Image { get; init; }

    [JsonPropertyName("sandbox_spec_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SandboxSpecId { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Error { get; init; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string> Metadata { get; init; }

    [JsonIgnore]
    public bool IsTerminal => TerminalStatuses.Contains(Status);

    [JsonIgnore]
    public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string FailureReason => !string.IsNullOrWhiteSpace(Error) ? Error : Message;
}
