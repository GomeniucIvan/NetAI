using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class SandboxInfoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("created_by_user_id")]
    public string CreatedByUserId { get; set; }

    [JsonPropertyName("sandbox_spec_id")]
    public string SandboxSpecId { get; set; }

    [JsonPropertyName("status")]
    public SandboxStatus Status { get; set; } = SandboxStatus.STARTING;

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }

    [JsonPropertyName("exposed_urls")]
    public IReadOnlyList<ExposedUrlDto> ExposedUrls { get; set; } = Array.Empty<ExposedUrlDto>();

    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; set; }

    [JsonPropertyName("workspace_path")]
    public string WorkspacePath { get; set; }

    [JsonPropertyName("runtime_hosts")]
    public IReadOnlyList<SandboxRuntimeHostDto> RuntimeHosts { get; set; } = Array.Empty<SandboxRuntimeHostDto>();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
