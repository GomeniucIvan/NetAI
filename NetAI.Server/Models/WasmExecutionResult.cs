namespace NetAI.Server.Models;

public sealed class WasmExecutionResult
{
    public int ExitCode { get; init; }

    public string Stdout { get; init; } = string.Empty;

    public string Stderr { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }
}
