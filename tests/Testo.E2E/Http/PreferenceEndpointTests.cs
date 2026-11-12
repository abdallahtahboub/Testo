using System.Net;
using Testo.E2E.Support;

namespace Testo.E2E.Http;

/// <summary>The form targets of the theme switch and the language switch.</summary>
public class PreferenceEndpointTests(AppFixture app) : HttpTest(app)
{
    private const string Theme = "/preferences/theme";
    private const string Culture = "/preferences/culture";

    private static Dictionary<string, string> Form(string field, string value, string returnUrl) =>
        new() { [field] = value, ["returnUrl"] = returnUrl };

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Theme_ExplicitChoice_IsStoredForAYearAndSendsTheVisitorBack(string choice)
    {
        using var response = await PostFormAsync(Theme, Form("theme", choice, "/de?x=1"));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/de?x=1", response.Headers.Location?.OriginalString);
        var cookie = Assert.Single(Header(response, "Set-Cookie"));
        Assert.StartsWith($"testo.theme={choice};", cookie, StringComparison.Ordinal);
        Assert.Contains("max-age=31536000", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("")]
    [InlineData("no-such-theme")]
    public async Task Theme_SystemOrUnknownValue_RemovesTheCookie(string choice)
    {
        using var response = await PostFormAsync(Theme, Form("theme", choice, "/en"));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var cookie = Assert.Single(Header(response, "Set-Cookie"));
        Assert.StartsWith("testo.theme=;", cookie, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("evil")]
    [InlineData("")]
    public async Task ReturnUrl_OutsideThisSite_FallsBackToTheStartPage(string returnUrl)
    {
        using var theme = await PostFormAsync(Theme, Form("theme", "dark", returnUrl));
        using var culture = await PostFormAsync(Culture, Form("culture", "de", returnUrl));

        Assert.Equal("/", theme.Headers.Location?.OriginalString);
        Assert.Equal("/", culture.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("de", "/en/pricing?plan=team", "/de/pricing?plan=team")]
    [InlineData("en", "/de", "/en")]
    [InlineData("DE", "/en", "/de")]
    [InlineData("de", "/app/settings", "/app/settings")]
    public async Task Culture_SupportedLanguage_IsStoredAndPublicUrlsChangeTheirPrefix(string language, string returnUrl, string expectedLocation)
    {
        using var response = await PostFormAsync(Culture, Form("culture", language, returnUrl));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        var cookie = Uri.UnescapeDataString(Assert.Single(Header(response, "Set-Cookie")));
        var expected = string.Equals(language, "de", StringComparison.OrdinalIgnoreCase) ? "c=de|uic=de" : "c=en|uic=en";
        Assert.StartsWith($".AspNetCore.Culture={expected};", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    public async Task Culture_UnsupportedLanguage_IsRejected(string language)
    {
        using var response = await PostFormAsync(Culture, Form("culture", language, "/en"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Header(response, "Set-Cookie"));
    }

    [Theory]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    [InlineData("Sec-Fetch-Site", "same-site")]
    [InlineData("Origin", "https://evil.example")]
    public async Task Post_FromAnotherWebsite_IsForbidden(string header, string value)
    {
        using var theme = await PostFormAsync(Theme, Form("theme", "dark", "/en"), (header, value));
        using var culture = await PostFormAsync(Culture, Form("culture", "de", "/en"), (header, value));

        Assert.Equal(HttpStatusCode.Forbidden, theme.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, culture.StatusCode);
        Assert.Empty(Header(theme, "Set-Cookie"));
        Assert.Empty(Header(culture, "Set-Cookie"));
    }

    [Fact]
    public async Task Post_FromThisWebsite_IsAccepted()
    {
        using var modernBrowser = await PostFormAsync(Theme, Form("theme", "dark", "/en"), ("Sec-Fetch-Site", "same-origin"));
        using var olderBrowser = await PostFormAsync(Theme, Form("theme", "dark", "/en"), ("Origin", App.BaseAddress.GetLeftPart(UriPartial.Authority)));

        Assert.Equal(HttpStatusCode.SeeOther, modernBrowser.StatusCode);
        Assert.Equal(HttpStatusCode.SeeOther, olderBrowser.StatusCode);
    }

    [Theory]
    [InlineData(Theme)]
    [InlineData(Culture)]
    public async Task Get_ChangesNothingAndShowsTheNotFoundPage(string path)
    {
        // The endpoints only accept form posts. Someone who opens the address gets a normal page.
        using var response = await GetAsync(path);
        var html = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Header(response, "Set-Cookie"));
        Assert.Contains("<h1", html, StringComparison.Ordinal);
    }

    private AppFixture App { get; } = app;
}
