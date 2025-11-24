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

public sealed record RuntimeErrorObservation(string Message) : RuntimeObservation;
