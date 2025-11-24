using System;

namespace NetAI.Api.Services.Sandboxes;

public class SandboxOrchestrationOptions
{
    public string ApiUrl { get; set; }

    public string ApiKey { get; set; }

    public string WebUrl { get; set; }

    public int ResourceFactor { get; set; } = 1;

    public int RunAsUser { get; set; } = 1000;

    public int RunAsGroup { get; set; } = 1000;

    public int FsGroup { get; set; } = 1000;

    public string RuntimeClass { get; set; }

    public TimeSpan StartSandboxTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
