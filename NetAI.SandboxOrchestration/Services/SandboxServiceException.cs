using System;
using System.Net;

namespace NetAI.SandboxOrchestration.Services;

public class SandboxServiceException : Exception
{
    public SandboxServiceException(
        string serviceName,
        string operation,
        HttpStatusCode statusCode,
        string message,
        string errorCode = null,
        Exception innerException = null)
        : base(message, innerException)
    {
        ServiceName = serviceName;
        Operation = operation;
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public string ServiceName { get; }

    public string Operation { get; }

    public HttpStatusCode StatusCode { get; }

    public string ErrorCode { get; }

    public string DerivedStatus
        => !string.IsNullOrWhiteSpace(ErrorCode)
            ? ErrorCode!
            : $"{ServiceName}:{(int)StatusCode}";
}
