namespace Testo.Web.Preferences;

/// <summary>
/// Rejects form posts that come from another website. It protects the preference endpoints
/// against cross-site request forgery without an antiforgery token.
/// </summary>
internal sealed class SameOriginFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        return IsSameOrigin(context.HttpContext.Request)
            ? await next(context)
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static bool IsSameOrigin(HttpRequest request)
    {
        // Current browsers state where a request comes from.
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0)
        {
            return fetchSite is "same-origin" or "none";
        }

        // Older browsers: compare the Origin header with our own host. A request without an
        // Origin header is not a form post from another website.
        var origin = request.Headers.Origin.ToString();
        return origin.Length == 0
            || (Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
                && string.Equals(originUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase));
    }
}
