namespace NetAI.Api.Authentication;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";
    public const string WwwAuthenticateHeaderValue = "ApiKey";
}
