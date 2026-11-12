using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Testo.Platform.Localization;

namespace Testo.Web.Localization;

/// <summary>
/// Takes the language of a public page from its URL: <c>/de/pricing</c> is German (ADR-0006).
/// URLs without a language prefix (the app) fall through to the next provider.
/// </summary>
internal sealed class UrlPrefixRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // When a request is re-executed to show the not-found or the error page, the URL the
        // visitor asked for still tells the language.
        var path = httpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
            ?? httpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path
            ?? httpContext.Request.Path.Value;

        return LocalizedPath.TryGetCulture(path, out var culture)
            ? Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture))
            : NullProviderCultureResult;
    }
}
