using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.RuntimeServer.Models
{
    public record CreateConversationRequestDto([property: JsonPropertyName("name")] string? Name = null);

    public class RuntimeConversationInitResult
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = "ok";

        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; init; } = string.Empty;

        [JsonPropertyName("conversation_status")]
        public string ConversationStatus { get; init; } = "CREATED";

        [JsonPropertyName("runtime_status")]
        public string RuntimeStatus { get; init; } = "READY";

        [JsonPropertyName("message")]
        public string Message { get; init; } = "Conversation created successfully";

        [JsonPropertyName("session_api_key")]
        public string? SessionApiKey { get; init; }

        [JsonPropertyName("runtime_id")]
        public string? RuntimeId { get; init; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; init; }
    }

    public class RuntimeConversationOperationResult
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = "ok";

        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; init; } = string.Empty;

        [JsonPropertyName("conversation_status")]
        public string ConversationStatus { get; init; } = "STARTED";

        [JsonPropertyName("runtime_status")]
        public string RuntimeStatus { get; init; } = "RUNNING";

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        [JsonPropertyName("session_api_key")]
        public string? SessionApiKey { get; init; }

        [JsonPropertyName("runtime_id")]
        public string? RuntimeId { get; init; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; init; }
    }

    public class RuntimeConversationEventDto
    {
        [JsonPropertyName("id")]
        public int EventId { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("type")]
        public string Type { get; init; } = "event";

        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class RuntimeConversationStateDto
    {
        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("runtime_status")]
        public string RuntimeStatus { get; init; } = string.Empty;

        [JsonPropertyName("session_api_key")]
        public string? SessionApiKey { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("last_updated_at")]
        public DateTimeOffset LastUpdatedAt { get; init; }
    }

    public class RuntimeConversationConfigDto
    {
        [JsonPropertyName("runtime_id")]
        public string? RuntimeId { get; init; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; init; }
    }

    public class RuntimeConversationVscodeUrlDto
    {
        [JsonPropertyName("vscode_url")]
        public string? VscodeUrl { get; init; }
    }

    public class RuntimeConversationEventsPageDto
    {
        [JsonPropertyName("events")]
        public IReadOnlyList<RuntimeConversationEventDto> Events { get; init; } = Array.Empty<RuntimeConversationEventDto>();

        [JsonPropertyName("has_more")]
        public bool HasMore { get; init; }
    }

    public class RuntimeConversationWebHostsDto
    {
        [JsonPropertyName("hosts")]
        public IReadOnlyDictionary<string, string> Hosts { get; init; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class RuntimeMicroagentInputDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;
    }

    public class RuntimeMicroagentDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;

        [JsonPropertyName("triggers")]
        public IReadOnlyList<string> Triggers { get; init; } = Array.Empty<string>();

        [JsonPropertyName("inputs")]
        public IReadOnlyList<RuntimeMicroagentInputDto> Inputs { get; init; }
            = Array.Empty<RuntimeMicroagentInputDto>();

        [JsonPropertyName("tools")]
        public IReadOnlyList<string> Tools { get; init; } = Array.Empty<string>();
    }

    public class RuntimeConversationMicroagentsResult
    {
        [JsonPropertyName("microagents")]
        public IReadOnlyList<RuntimeMicroagentDto> Microagents { get; init; }
            = Array.Empty<RuntimeMicroagentDto>();
    }

    public class RuntimeConversationFileSelectionResult
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("is_binary")]
        public bool IsBinary { get; init; }
    }

    public class RuntimeUploadSkippedFile
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;
    }

    public class RuntimeConversationUploadResult
    {
        [JsonPropertyName("uploaded_files")]
        public IReadOnlyList<string> UploadedFiles { get; init; }
            = Array.Empty<string>();

        [JsonPropertyName("skipped_files")]
        public IReadOnlyList<RuntimeUploadSkippedFile> SkippedFiles { get; init; }
            = Array.Empty<RuntimeUploadSkippedFile>();
    }

    public class RuntimeZipStreamResult
    {
        public required Stream Content { get; init; }

        public string FileName { get; init; } = "workspace.zip";

        public string ContentType { get; init; } = "application/zip";
    }

    public class RuntimeGitChangeResult
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;
    }

    public class RuntimeGitDiffResult
    {
        [JsonPropertyName("original")]
        public string? Original { get; init; }

        [JsonPropertyName("modified")]
        public string? Modified { get; init; }
    }

    public class RuntimeUploadedFile
    {
        public RuntimeUploadedFile(string fileName, Stream content, string? contentType)
        {
            FileName = fileName;
            Content = content;
            ContentType = contentType;
        }

        public string FileName { get; }

        public Stream Content { get; }

        public string? ContentType { get; }
    }
}
