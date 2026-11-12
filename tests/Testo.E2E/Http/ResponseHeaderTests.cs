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
