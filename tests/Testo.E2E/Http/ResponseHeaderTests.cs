using System.Net;
using Testo.E2E.Support;

namespace Testo.E2E.Http;

/// <summary>What the server promises in its response headers.</summary>
public class ResponseHeaderTests(AppFixture app) : HttpTest(app)
{
    [Theory]
    [InlineData("/en", "en")]
    [InlineData("/de", "de")]
    public async Task PublicPage_HasNoCookieAndMayBeKeptByTheBrowser(string path, string language)
    {
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Header(response, "Set-Cookie"));
        Assert.DoesNotContain(Header(response, "Cache-Control"), value => value.Contains("no-store", StringComparison.OrdinalIgnoreCase));
        Assert.Equal([language], Header(response, "Content-Language"));
    }

    [Fact]
    public async Task PublicPage_ServerRendersTheThemeFromTheCookie()
    {
        using var response = await GetAsync("/en", ("Cookie", "testo.theme=dark"));
        var html = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Contains("<html lang=\"en\" data-theme=\"dark\">", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"color-scheme\" content=\"dark\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicPage_WithoutThemeCookie_LeavesTheChoiceToTheSystem()
    {
        using var response = await GetAsync("/en");
        var html = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"color-scheme\" content=\"light dark\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-DE,de;q=0.9,en;q=0.8", "/de")]
    [InlineData("en-GB,en;q=0.9", "/en")]
    [InlineData("ja", "/en")]
    public async Task BareAddress_RedirectsByLanguageAndTellsCachesWhy(string acceptLanguage, string expected)
    {
        using var response = await GetAsync("/", ("Accept-Language", acceptLanguage));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
        Assert.Contains("Accept-Language", string.Join(',', Header(response, "Vary")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cookie", string.Join(',', Header(response, "Vary")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BareAddress_PreferenceCookie_WinsOverTheBrowsersLanguage()
    {
        using var response = await GetAsync("/", ("Accept-Language", "en-US"), ("Cookie", ".AspNetCore.Culture=c%3Dde%7Cuic%3Dde"));

        Assert.Equal("/de", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task QueryString_DoesNotChangeTheLanguage()
    {
        // The built-in "?culture=" switch is off: every page has exactly one address per language.
        using var response = await GetAsync("/en?culture=de&ui-culture=de");
        var html = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Contains("<html lang=\"en\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/en")]
    [InlineData("/app")]
    [InlineData("/en/no-such-page")]
    [InlineData("/_content/Testo.UI.Shared/css/tokens.css")]
    public async Task AnyResponse_CarriesTheSecurityHeaders(string path)
    {
        using var response = await GetAsync(path);

        Assert.Equal(["nosniff"], Header(response, "X-Content-Type-Options"));
        Assert.Equal(["DENY"], Header(response, "X-Frame-Options"));
        Assert.Equal(["strict-origin-when-cross-origin"], Header(response, "Referrer-Policy"));
        Assert.Single(Header(response, "Permissions-Policy"));
        var policy = Assert.Single(Header(response, "Content-Security-Policy"));
        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicPage_Policy_AllowsNothingInline()
    {
        using var response = await GetAsync("/en");
        var policy = Assert.Single(Header(response, "Content-Security-Policy"));

        Assert.Contains("script-src 'self';", policy, StringComparison.Ordinal);
        Assert.Contains("style-src 'self';", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("nonce", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppPage_Policy_AllowsOnlyTheScriptWithThisRequestsNonce()
    {
        // /app needs a session (D-1); without one the response is a redirect with no page to
        // carry a nonce.
        var cookies = await SignInAsync();

        using var first = await GetAsync("/app", ("Cookie", cookies));
        using var second = await GetAsync("/app", ("Cookie", cookies));
        var policy = Assert.Single(Header(first, "Content-Security-Policy"));
        var html = await first.Content.ReadAsStringAsync(Cancellation);

        var nonce = System.Text.RegularExpressions.Regex.Match(policy, "script-src 'self' 'nonce-([^']+)'").Groups[1].Value;
        // 128 bits, in characters that are written the same in a header and in HTML.
        Assert.Matches("^[0-9A-F]{32}$", nonce);
        Assert.Contains($"nonce=\"{nonce}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", policy[..policy.IndexOf("style-src", StringComparison.Ordinal)], StringComparison.Ordinal);
        // A nonce is only worth something if it is new for every request.
        Assert.DoesNotContain(nonce, Assert.Single(Header(second, "Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("inter-4.001-latin.woff2")]
    [InlineData("inter-4.001-latin-ext.woff2")]
    [InlineData("jetbrains-mono-2.211-latin.woff2")]
    [InlineData("jetbrains-mono-2.211-latin-ext.woff2")]
    public async Task Font_IsServedFromOurOriginAndCachedForAYear(string file)
    {
        using var response = await GetAsync("/_content/Testo.UI.Shared/fonts/" + file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["font/woff2"], Header(response, "Content-Type"));
        Assert.Equal(["public, max-age=31536000, immutable"], Header(response, "Cache-Control"));
    }

    [Fact]
    public async Task PublicPage_PreloadsTheTextFontItsStyleSheetUses()
    {
        using var page = await GetAsync("/en");
        var html = await page.Content.ReadAsStringAsync(Cancellation);
        var styleSheetUrl = System.Text.RegularExpressions.Regex.Match(html, "href=\"(_content/Testo\\.UI\\.Shared/css/fonts[^\"]*\\.css)\"").Groups[1].Value;
        using var styleSheet = await GetAsync("/" + styleSheetUrl);
        var css = await styleSheet.Content.ReadAsStringAsync(Cancellation);

        // Both must name the same file, or the browser downloads the font twice.
        Assert.Contains("rel=\"preload\" href=\"_content/Testo.UI.Shared/fonts/inter-4.001-latin.woff2\" as=\"font\" type=\"font/woff2\" crossorigin", html, StringComparison.Ordinal);
        Assert.Contains("url(\"../fonts/inter-4.001-latin.woff2\")", css, StringComparison.Ordinal);
    }
}
