using Microsoft.Net.Http.Headers;

namespace Testo.Web.StaticAssets;

/// <summary>Long-lived browser caching for the self-hosted font files.</summary>
internal static class FontCachingExtensions
{
    private const string FontsPath = "/_content/Testo.UI.Shared/fonts";

    /// <summary>
    /// Lets browsers keep the font files for a year without asking again.
    /// </summary>
    /// <remarks>
    /// Style sheets reference fonts by their plain file name, which the static asset
    /// pipeline serves with "no-cache". Every page load would then re-validate the fonts and
    /// briefly show the fallback font. The font file names carry the font version instead
    /// (see THIRD-PARTY-NOTICES.md), so caching them as immutable is safe.
    /// </remarks>
    public static IApplicationBuilder UseImmutableFontCaching(this IApplicationBuilder app)
    {
        return app.Use((context, next) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments(FontsPath, StringComparison.OrdinalIgnoreCase)
                && path.Value is { } value
                && value.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.OnStarting(() =>
                {
                    if (context.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status304NotModified)
                    {
                        context.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
                    }

                    return Task.CompletedTask;
                });
            }

            return next(context);
        });
    }
}
