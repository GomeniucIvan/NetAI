using System;

namespace NetAI.Api.Services.Sandboxes;

public class SandboxOrchestrationException : Exception
{
    public SandboxOrchestrationException(string message)
        : base(message)
    {
    }

    public SandboxOrchestrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
