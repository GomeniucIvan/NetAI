namespace NetAI.Api.Models.Installation;

public class InstallationResult
{
    private InstallationResult()
    {
    }

    public bool Success { get; init; }
        = false;

    public int StatusCode { get; init; }
        = StatusCodes.Status500InternalServerError;

    public string Message { get; init; }

    public string Error { get; init; }

    public static InstallationResult SuccessResult(int statusCode, string message)
        => new()
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Error = null
        };

    public static InstallationResult Failure(int statusCode, string error)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = error,
            Message = null
        };
}
