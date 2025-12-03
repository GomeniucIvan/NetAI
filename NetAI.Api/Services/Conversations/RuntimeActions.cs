namespace NetAI.Api.Services.Conversations;

public interface IRuntimeAction
{
}

public sealed class RuntimeFileReadAction : IRuntimeAction
{
    public RuntimeFileReadAction(string path)
    {
        Path = path;
    }

    public string Path { get; }
}

public abstract record RuntimeObservation;

public sealed record RuntimeFileReadObservation(string Content) : RuntimeObservation;

public sealed record RuntimeFileEditObservation(
    string Path,
    string? Content,
    string? Diff,
    int? StartLine,
    int? EndLine,
    bool? LintEnabled) : RuntimeObservation;

public sealed record RuntimeErrorObservation(string Message) : RuntimeObservation
{
    public string? Code { get; init; }
}

public enum RuntimeFileEditOperation
{
    View,
    Insert,
    Replace,
    Diff,
    ToggleLint
}

public sealed class RuntimeFileEditAction : IRuntimeAction
{
    public RuntimeFileEditAction(
        string path,
        RuntimeFileEditOperation operation,
        int? startLine = null,
        int? endLine = null,
        string? content = null,
        bool? lintEnabled = null)
    {
        Path = path;
        Operation = operation;
        StartLine = startLine;
        EndLine = endLine;
        Content = content;
        LintEnabled = lintEnabled;
    }

    public string Path { get; }

    public RuntimeFileEditOperation Operation { get; }

    public int? StartLine { get; }

    public int? EndLine { get; }

    public string? Content { get; }

    public bool? LintEnabled { get; }
}
