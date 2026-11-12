using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Testo.Platform;
using Testo.Platform.Localization;
using Testo.UI.Shared.Theming;

namespace Testo.Web.Preferences;

/// <summary>
/// The form targets of the theme switch and the language switch. Each stores the choice in a
/// cookie and sends the visitor back to the page they came from.
/// </summary>
/// <remarks>
/// The two switches sit in the header of every page. A form with an antiforgery token would
/// set a cookie and forbid caching on every public page, so these endpoints work without
/// one. That is safe here: they only store a display preference, and
/// <see cref="SameOriginFilter"/> rejects posts from other websites.
/// </remarks>
internal static class PreferenceEndpoints
{
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    public static IEndpointRouteBuilder MapPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Routes.ThemePreference, SetTheme)
            .DisableAntiforgery()
            .AddEndpointFilter<SameOriginFilter>();

        endpoints.MapPost(Routes.CulturePreference, SetCulture)
            .DisableAntiforgery()
            .AddEndpointFilter<SameOriginFilter>();

        return endpoints;
    }

    private static IResult SetTheme(HttpContext context, [FromForm] string? theme, [FromForm] string? returnUrl)
    {
        var choice = ThemeCookie.Parse(theme);
        if (choice == ThemeChoice.System)
        {
            // "Same as system" is the default and needs no cookie.
            context.Response.Cookies.Delete(ThemeCookie.Name, new CookieOptions { Path = "/" });
        }
        else
        {
            // Readable by script: js/theme.js writes the same cookie when it switches instantly.
            context.Response.Cookies.Append(ThemeCookie.Name, ThemeCookie.ToValue(choice), CreateCookieOptions(context, httpOnly: false));
        }

        return SeeOther(context, ToLocalUrl(returnUrl));
    }

    private static IResult SetCulture(HttpContext context, [FromForm] string? culture, [FromForm] string? returnUrl)
    {
        var language = SupportedCultures.All.FirstOrDefault(supported => string.Equals(supported, culture, StringComparison.OrdinalIgnoreCase));
        if (language is null)
        {
            return Results.BadRequest();
        }

        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language)),
            CreateCookieOptions(context, httpOnly: true));

        // A public page moves to the same page under the other language's URL.
        return SeeOther(context, LocalizedPath.WithCulture(ToLocalUrl(returnUrl), language));
    }

    private static CookieOptions CreateCookieOptions(HttpContext context, bool httpOnly) => new()
    {
        Path = "/",
        MaxAge = CookieLifetime,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        HttpOnly = httpOnly,
        IsEssential = true,
    };

    /// <summary>"303 See Other": the browser fetches the page with GET after the form post.</summary>
    private static IResult SeeOther(HttpContext context, string url)
    {
        context.Response.Headers.Location = url;
        return Results.StatusCode(StatusCodes.Status303SeeOther);
    }

    /// <summary>Only ever redirect inside this site; anything else falls back to the start page.</summary>
    private static string ToLocalUrl(string? returnUrl) => IsLocalUrl(returnUrl) ? returnUrl : "/";

    private static bool IsLocalUrl([NotNullWhen(true)] string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.Any(char.IsControl);
}
