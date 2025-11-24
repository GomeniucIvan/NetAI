using System.Collections.Generic;

namespace NetAI.Api.Services.Sandboxes;

public class DefaultSandboxSpecOptions
{
    //todo
    public const string DefaultImage = "ghcr.io/all-hands-ai/agent-server:ab36fd6-python";

    public string Id { get; set; } = DefaultImage;

    public List<string> Command { get; set; } = new();

    public Dictionary<string, string> InitialEnv { get; set; } = new();

    //todo setting - projects
    public string WorkingDir { get; set; } = "/workspace";
}
