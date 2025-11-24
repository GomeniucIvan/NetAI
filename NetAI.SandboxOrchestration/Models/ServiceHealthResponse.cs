namespace NetAI.SandboxOrchestration.Models;

public class ServiceHealthResponse
{
    public string Status { get; init; }
    public bool IsHealthy { get; init; }
    public string Message { get; init; }
}
