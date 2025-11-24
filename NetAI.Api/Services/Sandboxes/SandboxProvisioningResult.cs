using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public record class SandboxProvisioningResult(
    SandboxStatus Status,
    string SessionApiKey,
    string RuntimeId,
    string RuntimeUrl,
    string WorkspacePath,
    IReadOnlyList<ExposedUrlDto> ExposedUrls,
    IReadOnlyList<SandboxRuntimeHostDto> RuntimeHosts,
    string RuntimeStateJson);
