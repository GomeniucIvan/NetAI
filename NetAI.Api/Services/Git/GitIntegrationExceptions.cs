namespace NetAI.Api.Services.Git;

public class GitAuthorizationException : Exception
{
    public GitAuthorizationException(string message)
        : base(message)
    {
    }
}

public class GitResourceNotFoundException : Exception
{
    public GitResourceNotFoundException(string message)
        : base(message)
    {
    }
}
