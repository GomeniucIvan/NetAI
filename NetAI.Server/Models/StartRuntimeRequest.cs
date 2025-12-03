namespace NetAI.Server.Models;

public sealed class StartRuntimeRequest
{
    public string ModulePath { get; set; } = string.Empty;

    public string? WorkspacePath { get; set; }

    public List<string> Arguments { get; set; } = new();

    public int TimeoutSeconds { get; set; } = 120;
}
