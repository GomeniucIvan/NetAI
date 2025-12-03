using System.ComponentModel.DataAnnotations;

namespace NetAI.Server.Options;

public sealed class AgentRuntimeOptions
{
    [Required]
    public string Provider { get; set; } = "netai";

    public bool EnableTooling { get; set; } = true;

    public IReadOnlyList<string> ToolAllowList { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> ToolDenyList { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> MicroagentRegistryPaths { get; set; } = Array.Empty<string>();

    public bool UseInMemoryStub { get; set; } = false;
}
