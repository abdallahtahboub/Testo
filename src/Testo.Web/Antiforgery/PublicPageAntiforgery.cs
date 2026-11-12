using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components;
using Testo.Platform.Rendering;

namespace Testo.Web.Antiforgery;

/// <summary>
/// Keeps public pages without forms free of the antiforgery cookie.
/// </summary>
/// <remarks>
/// Blazor issues an antiforgery token for every page it renders on the server, whether the
/// page has a form or not. Issuing a token sets a cookie and marks the response as
/// "no-store". For the public website that would mean a cookie on the very first visit and
/// pages the browser may not keep for back/forward navigation. This wrapper skips the token
/// for statically rendered pages that do not declare a form with <see cref="HasFormAttribute"/>.
/// App pages and pages with forms are untouched, and validation of incoming posts is never
/// skipped.
/// </remarks>
internal sealed class PublicPageAntiforgery(IAntiforgery inner) : IAntiforgery
{
    private static readonly AntiforgeryTokenSet NoTokens = new(
        requestToken: null,
        cookieToken: null,
        formFieldName: "__RequestVerificationToken",
        headerName: null);

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) =>
        IsFormlessPublicPage(httpContext) ? NoTokens : inner.GetAndStoreTokens(httpContext);

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
        IsFormlessPublicPage(httpContext) ? NoTokens : inner.GetTokens(httpContext);

    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => inner.IsRequestValidAsync(httpContext);

    public Task ValidateRequestAsync(HttpContext httpContext) => inner.ValidateRequestAsync(httpContext);

    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
        if (!IsFormlessPublicPage(httpContext))
        {
            inner.SetCookieTokenAndHeader(httpContext);
        }
    }

    private static bool IsFormlessPublicPage(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var metadata = httpContext.GetEndpoint()?.Metadata;
        return metadata?.GetMetadata<ExcludeFromInteractiveRoutingAttribute>() is not null
            && metadata.GetMetadata<HasFormAttribute>() is null;
    }
}
