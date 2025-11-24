namespace NetAI.RuntimeGateway.Services;

public sealed class OpenHandsOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3000";

    public string ApiPrefix { get; set; } = "/api";

    public string SocketUrl { get; set; } = "http://localhost:3000/socket.io/";

    public int SocketConnectionTimeoutSeconds { get; set; } = 30;
}
