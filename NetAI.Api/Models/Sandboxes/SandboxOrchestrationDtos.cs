using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Sandboxes;

public class SandboxOrchestrationStartRequestDto
{
    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("sandbox_spec_id")]
    public string SandboxSpecId { get; set; }

    [JsonPropertyName("sandbox_spec")]
    public SandboxSpecInfoDto SandboxSpec { get; set; }
}

public class SandboxOrchestrationResumeRequestDto
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }
}

public class SandboxOrchestrationPauseRequestDto
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }
}

public class SandboxOrchestrationSessionDto
{
    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("sandbox_spec_id")]
    public string SandboxSpecId { get; set; }

    [JsonPropertyName("status")]
    public SandboxStatus Status { get; set; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }

    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }

    [JsonPropertyName("runtime_url")]
    public string RuntimeUrl { get; set; }

    [JsonPropertyName("workspace_path")]
    public string WorkspacePath { get; set; }

    [JsonPropertyName("exposed_urls")]
    public IReadOnlyList<ExposedUrlDto> ExposedUrls { get; set; } = Array.Empty<ExposedUrlDto>();

    [JsonPropertyName("runtime_hosts")]
    public IReadOnlyList<SandboxRuntimeHostDto> RuntimeHosts { get; set; } = Array.Empty<SandboxRuntimeHostDto>();

    [JsonPropertyName("runtime_state_json")]
    public string RuntimeStateJson { get; set; }
}
