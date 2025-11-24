using System.Net;

namespace NetAI.Api.Services.Conversations;

public sealed class RuntimeConversationGatewayException : Exception
{
    public RuntimeConversationGatewayException(HttpStatusCode statusCode, string message, string responseBody = null)
        : base(string.IsNullOrWhiteSpace(message) ? $"Runtime gateway request failed with status {(int)statusCode}." : message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }
}
