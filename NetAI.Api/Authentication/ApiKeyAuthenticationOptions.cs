using Microsoft.AspNetCore.Authentication;

namespace NetAI.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = ApiKeyAuthenticationDefaults.HeaderName;
}
