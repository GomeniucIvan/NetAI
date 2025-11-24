using System;
using Microsoft.AspNetCore.Http;

namespace NetAI.Api.Services.Installation;

public static class InstallationRequestMatcher
{
    private static readonly PathString InstallApiPath = new("/api/install");
    private static readonly PathString OptionsApiPath = new("/api/options");
    private static readonly PathString InstallUiPath = new("/install");

    public static bool AllowsBypass(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            return true;
        }

        PathString path = context.Request.Path;
        if (!path.HasValue)
        {
            return false;
        }

        if (path.StartsWithSegments(InstallApiPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWithSegments(OptionsApiPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWithSegments(InstallUiPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static bool ShouldRedirectToInstall(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return false;
        }

        PathString path = context.Request.Path;
        if (!path.HasValue)
        {
            return true;
        }

        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
