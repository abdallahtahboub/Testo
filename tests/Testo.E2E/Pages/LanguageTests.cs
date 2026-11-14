using Microsoft.Playwright;
using Testo.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// Language handling (ADR-0006): the URL prefix decides on the public site, the preference
/// cookie decides in the app, and the browser's language is the first guess.
/// </summary>
public class LanguageTests(AppFixture app) : BrowserTest(app)
{
    private const string CultureCookie = ".AspNetCore.Culture";

    [Theory]
    [InlineData("en-US", "/en")]
    [InlineData("de-DE", "/de")]
    [InlineData("de-AT", "/de")]
    [InlineData("fr-FR", "/en")]
    public async Task BareAddress_LeadsToTheHomePageInTheBrowsersLanguage(string locale, string expectedPath)
    {
        var page = await OpenAsync("/", options => options.Locale = locale);

        Assert.Equal(expectedPath, page.Path);
    }

    [Fact]
    public async Task UrlPrefix_WinsOverTheBrowsersLanguage()
    {
        var page = await OpenAsync("/en", options => options.Locale = "de-DE");

        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Quality you can see, from requirement to release");
    }

    [Fact]
    public async Task SwitchingLanguage_OnAPublicPage_MovesToTheSamePageInTheOtherLanguage()
    {
        var page = await OpenAsync("/en");
        await Expect(page.Page.LanguageButton("English")).ToHaveAttributeAsync("aria-current", "true");

        await page.Page.LanguageButton("Deutsch").ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(App.Url("/de"));
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", "de");
        await Expect(page.Page.LanguageButton("Deutsch")).ToHaveAttributeAsync("aria-current", "true");

        var cookie = await page.Page.CookieAsync(CultureCookie);
        Assert.Equal("c=de|uic=de", Uri.UnescapeDataString(cookie?.Value ?? ""));
        Assert.True(cookie?.HttpOnly);
        Assert.Equal(SameSiteAttribute.Lax, cookie?.SameSite);

        // The choice is remembered: the bare address now leads to German, although the browser is English.
        await page.Page.GotoAsync(App.Url("/"));
        Assert.Equal("/de", page.Path);
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SwitchingLanguage_KeepsThePathBehindThePrefix()
    {
        var page = await OpenAsync("/en/no-such-page?from=test");

        await page.Page.LanguageButton("Deutsch").ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(App.Url("/de/no-such-page?from=test"));
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Seite nicht gefunden");
    }

    [Fact]
    public async Task App_FirstVisit_UsesTheBrowsersLanguageAndItsFormats()
    {
        var page = await OpenSignedInAsync("/app", options => options.Locale = "de-DE");
        await page.Page.WaitUntilInteractiveAsync();

        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", "de");
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Vorschau der App-Shell");
        await Expect(page.Page.GetByTestId("language")).ToHaveTextAsync("Deutsch");
        // German number format: dot for thousands, comma for decimals.
        await Expect(page.Page.GetByTestId("formats")).ToContainTextAsync("1.234.567,89");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task SwitchingLanguage_InTheApp_ReloadsInThatLanguageAndRemembersIt()
    {
        var page = await OpenSignedInAsync("/app");
        await page.Page.WaitUntilInteractiveAsync();
        await Expect(page.Page.GetByTestId("formats")).ToContainTextAsync("1,234,567.89");

        await page.Page.LanguageButton("Deutsch").ClickAsync();

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Vorschau der App-Shell");
        await page.Page.WaitUntilInteractiveAsync();
        Assert.Equal("/app", page.Path);
        // The interactive session uses the same language as the first server-rendered response.
        await Expect(page.Page.GetByTestId("language")).ToHaveTextAsync("Deutsch");
        await Expect(page.Page.GetByTestId("formats")).ToContainTextAsync("1.234.567,89");

        await page.Page.ReloadAsync();
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", "de");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task TheFooterSwitch_WorksAsWellAsTheHeaderOne()
    {
        // A public page carries the switch twice, which is what made three tests ambiguous when
        // the footer arrived. Scoping those to the header would have quietly dropped the footer
        // from coverage, so this covers it: the second switch is a real control, not decoration.
        var page = await OpenAsync("/en/pricing");

        await Expect(page.Page.FooterLanguageButton("English")).ToHaveAttributeAsync("aria-current", "true");

        await page.Page.FooterLanguageButton("Deutsch").ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(App.Url("/de/pricing"));
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", "de");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task LanguageButtons_AreNamedInTheirOwnLanguage()
    {
        var page = await OpenAsync("/en");

        await Expect(page.Page.LanguageButton("English")).ToHaveAttributeAsync("lang", "en");
        await Expect(page.Page.LanguageButton("Deutsch")).ToHaveAttributeAsync("lang", "de");
    }
}
