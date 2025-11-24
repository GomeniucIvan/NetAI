using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public interface ISandboxOrchestrationClient
{
    Task<SandboxProvisioningResult> StartSandboxAsync(
        string sandboxId,
        SandboxSpecInfoDto spec,
        CancellationToken cancellationToken);

    Task<SandboxProvisioningResult> ResumeSandboxAsync(
        string sandboxId,
        string runtimeId,
        CancellationToken cancellationToken);

    Task<bool> PauseSandboxAsync(
        string sandboxId,
        string runtimeId,
        CancellationToken cancellationToken);
}
