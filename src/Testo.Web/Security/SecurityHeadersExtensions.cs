using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;

namespace Testo.Web.Security;

/// <summary>Response headers that harden every page against common web attacks.</summary>
internal static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds the security headers to every response. The content security policy has two
    /// levels: public pages allow nothing inline at all; app pages additionally allow inline
    /// styles (the Fluent UI components need them) and the one script that carries the
    /// request's nonce.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // "dotnet watch" injects a script that talks to the tooling on another local port.
        var developmentSources = environment.IsDevelopment() ? " ws://localhost:* wss://localhost:* http://localhost:*" : "";

        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
                headers.ContentSecurityPolicy = BuildPolicy(context, developmentSources);
                return Task.CompletedTask;
            });

            return next(context);
        });
    }

    private static string BuildPolicy(HttpContext context, string developmentSources)
    {
        var (script, style) = IsAppPage(context)
            ? ($"'self' 'nonce-{context.RequestServices.GetRequiredService<CspNonce>().Value}'", "'self' 'unsafe-inline'")
            : ("'self'", "'self'");

        return string.Join(
            "; ",
            "default-src 'self'",
            $"script-src {script}",
            $"style-src {style}",
            "img-src 'self' data:",
            "font-src 'self'",
            $"connect-src 'self'{developmentSources}",
            "form-action 'self'",
            "base-uri 'self'",
            "frame-ancestors 'none'",
            "object-src 'none'");
    }

    /// <summary>App pages are the Razor pages that are not excluded from interactive routing.</summary>
    private static bool IsAppPage(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata;
        return metadata?.GetMetadata<ComponentTypeMetadata>() is not null
            && metadata.GetMetadata<ExcludeFromInteractiveRoutingAttribute>() is null;
    }
}
