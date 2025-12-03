using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NetAI.RuntimeServer.Options;

namespace NetAI.RuntimeServer.Middleware;

public class SessionApiKeyMiddleware
{
    private const string SessionApiKeyHeader = "X-Session-API-Key";

    private readonly RequestDelegate _next;
    private readonly string? _configuredApiKey;
    private readonly ILogger<SessionApiKeyMiddleware> _logger;

    public SessionApiKeyMiddleware(
        RequestDelegate next,
        IOptions<SessionApiKeyOptions> options,
        ILogger<SessionApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _configuredApiKey = options.Value.SessionApiKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_configuredApiKey) || IsHealthCheckRequest(context))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(SessionApiKeyHeader, out var providedKey) ||
            !string.Equals(providedKey, _configuredApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected request with missing or invalid session API key for {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid session API key." });
            return;
        }

        await _next(context);
    }

    private static bool IsHealthCheckRequest(HttpContext context)
    {
        return string.Equals(context.Request.Path.Value, "/healthz", StringComparison.OrdinalIgnoreCase);
    }
}
