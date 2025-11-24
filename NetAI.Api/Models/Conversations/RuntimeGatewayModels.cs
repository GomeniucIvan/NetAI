using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Models.Conversations;

public class RuntimeConversationInitRequest
{
    [JsonPropertyName("conversation")]
    public CreateConversationRequestDto Conversation { get; init; } = new();

    [JsonPropertyName("sandbox_connection")]
    public SandboxConnectionInfoDto SandboxConnection { get; init; }

    [JsonPropertyName("provider_tokens")]
    public IDictionary<string, string> ProviderTokens { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public class RuntimeConversationEventDto
{
    [JsonPropertyName("event_id")]
    public int EventId { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("payload")]
    public string PayloadJson { get; init; }
}

public class RuntimeConversationInitResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "error";

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; }

    [JsonPropertyName("conversation_status")]
    public string ConversationStatus { get; init; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; init; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; init; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; init; }

    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; init; }

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; init; }

    [JsonPropertyName("vscode_url")]
    public string VscodeUrl { get; init; }

    [JsonPropertyName("events")]
    public IReadOnlyList<RuntimeConversationEventDto> Events { get; init; }
        = Array.Empty<RuntimeConversationEventDto>();

    [JsonPropertyName("hosts")]
    public IReadOnlyList<SandboxRuntimeHostDto> Hosts { get; init; }
        = Array.Empty<SandboxRuntimeHostDto>();

    [JsonPropertyName("providers")]
    public IReadOnlyList<string> Providers { get; init; }
        = Array.Empty<string>();
}

public class RuntimeConversationOperationRequest
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; init; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; init; }

    [JsonPropertyName("providers")]
    public IReadOnlyList<string> Providers { get; init; }
        = Array.Empty<string>();

    [JsonPropertyName("provider_tokens")]
    public IDictionary<string, string> ProviderTokens { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public class RuntimeConversationOperationResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "error";

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; init; }

    [JsonPropertyName("conversation_status")]
    public string ConversationStatus { get; init; }

    [JsonPropertyName("runtime_status")]
    public string RuntimeStatus { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; }

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; init; }

    [JsonPropertyName("vscode_url")]
    public string VscodeUrl { get; init; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; init; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; init; }

    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; init; }

    [JsonPropertyName("hosts")]
    public IReadOnlyList<SandboxRuntimeHostDto> Hosts { get; init; }
        = Array.Empty<SandboxRuntimeHostDto>();

    [JsonPropertyName("providers")]
    public IReadOnlyList<string> Providers { get; init; }
        = Array.Empty<string>();

    [JsonPropertyName("is_placeholder")]
    public bool IsPlaceholder { get; init; }
        = false;

    public bool IsSuccess()
    {
        return string.Equals(Status, "ok", StringComparison.OrdinalIgnoreCase);
    }
}

public class RuntimeConversationEventRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public JsonElement Payload { get; init; }
        = default;
}

public class RuntimeConversationMessageRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public string Message { get; init; } = string.Empty;

    public string Source { get; init; }
        = null;
}

public class RuntimeConversationEventsRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public int StartId { get; init; }
        = 0;

    public int? EndId { get; init; }
        = null;

    public bool Reverse { get; init; }
        = false;

    public int? Limit { get; init; }
        = null;
}

public class RuntimeConversationEventsResult
{
    public IReadOnlyList<JsonElement> Events { get; init; } = Array.Empty<JsonElement>();

    public bool HasMore { get; init; }
        = false;
}

public sealed class RuntimeConversationMetadataRequest
{
    public string ConversationUrl { get; init; }

    public string SessionApiKey { get; init; }
    public string Host { get; init; }
}

public sealed class RuntimeConversationMicroagentsRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;
}

public sealed class RuntimeConversationMicroagentsResult
{
    [JsonPropertyName("microagents")]
    public IReadOnlyList<MicroagentDto> Microagents { get; init; }
        = Array.Empty<MicroagentDto>();
}

public sealed class RuntimeConversationFileListRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public string Path { get; init; }
        = null;
}

public sealed class RuntimeConversationFileSelectionRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public string File { get; init; } = string.Empty;
}

public sealed class RuntimeConversationFileSelectionResult
{
    public string Code { get; init; }
        = null;

    public bool IsBinary { get; init; }
        = false;

    public string Error { get; init; }
        = null;
}

public sealed class RuntimeConversationUploadFile
{
    public required string FileName { get; init; }
        = string.Empty;

    public required Stream Content { get; init; }
        = Stream.Null;

    public string ContentType { get; init; }
        = null;
}

public sealed class RuntimeConversationUploadRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public IReadOnlyList<RuntimeConversationUploadFile> Files { get; init; }
        = Array.Empty<RuntimeConversationUploadFile>();
}

public sealed class RuntimeConversationUploadResult
{
    public IReadOnlyList<string> UploadedFiles { get; init; }
        = Array.Empty<string>();

    public IReadOnlyList<RuntimeUploadSkippedFile> SkippedFiles { get; init; }
        = Array.Empty<RuntimeUploadSkippedFile>();
}

public sealed class RuntimeUploadSkippedFile
{
    public string Name { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeConversationZipRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;
}

public sealed class RuntimeZipStreamResult
{
    public required Stream Content { get; init; }
        = Stream.Null;

    public string FileName { get; init; } = "workspace.zip";

    public string ContentType { get; init; } = "application/zip";
}

public sealed class RuntimeConversationGitChangesRequest
{
    public string ConversationUrl { get; init; }
    public string SessionApiKey { get; init; }
    public string Host { get; init; }
}

public sealed class RuntimeConversationGitDiffRequest
{
    public string ConversationUrl { get; init; } = string.Empty;

    public string SessionApiKey { get; init; }
        = null;

    public string Path { get; init; } = string.Empty;
}

public sealed class RuntimeGitChangeResult
{
    public string Path { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}

public sealed class RuntimeGitDiffResult
{
    public string Original { get; init; }
        = null;

    public string Modified { get; init; }
        = null;
}
