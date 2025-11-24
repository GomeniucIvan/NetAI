using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NetAI.Api.Services.Keys;

namespace NetAI.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder, clock)
    {
        _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryGetApiKey(out string apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        ApiKeyValidationResult validation = await _apiKeyService
            .ValidateApiKeyAsync(apiKey, Context.RequestAborted)
            .ConfigureAwait(false);

        if (!validation.Success || validation.ApiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validation.ApiKey.Id.ToString()),
            new(ClaimTypes.Name, validation.ApiKey.Name)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = ApiKeyAuthenticationDefaults.WwwAuthenticateHeaderValue;
        return base.HandleChallengeAsync(properties);
    }

    private bool TryGetApiKey(out string apiKey)
    {
        if (Request.Headers.TryGetValue(Options.HeaderName, out StringValues headerValues))
        {
            apiKey = ExtractHeaderValue(headerValues);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return true;
            }
        }

        if (Request.Headers.TryGetValue("Authorization", out headerValues))
        {
            apiKey = ExtractAuthorizationHeader(headerValues);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return true;
            }
        }

        apiKey = null;
        return false;
    }

    private static string ExtractHeaderValue(StringValues values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string ExtractAuthorizationHeader(StringValues values)
    {
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            const string bearerPrefix = "Bearer ";
            if (value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string token = value[bearerPrefix.Length..].Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
        }

        return null;
    }
}
