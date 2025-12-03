using System.Text.Json.Serialization;

namespace NetAI.Api.Services.Conversations;

public sealed class RuntimeConversationFileEditRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string? SessionApiKey { get; init; }

    public RuntimeFileEditAction Action { get; init; } = default!;
}

public sealed class RuntimeConversationFileEditResult
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("diff")]
    public string? Diff { get; init; }

    [JsonPropertyName("start_line")]
    public int? StartLine { get; init; }

    [JsonPropertyName("end_line")]
    public int? EndLine { get; init; }

    [JsonPropertyName("lint_enabled")]
    public bool? LintEnabled { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }
}
