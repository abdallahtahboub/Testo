using Microsoft.Playwright;
using Testo.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>What must hold for every public page: static, light, cookie-free and indexable per language.</summary>
public class PublicSiteTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task PublicPage_Loads_WithoutServerConnectionAndWithoutBlazor()
    {
        var page = await OpenAsync("/en");
        await page.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Empty(page.WebSockets);
        Assert.DoesNotContain(page.Requests, request => request.Contains("blazor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(page.Requests, request => request.Contains("FluentUI", StringComparison.OrdinalIgnoreCase));
        page.AssertNoProblems();
    }

    [Fact]
    public async Task PublicPage_PlainVisit_SetsNoCookie()
    {
        var page = await OpenAsync("/en");
        await page.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Empty(await page.Page.Context.CookiesAsync());
    }

    [Fact]
    public async Task PublicPage_Head_NamesTheSamePageInEveryLanguage()
    {
        var page = await OpenAsync("/de");

        await Expect(page.Page.Locator("link[rel=canonical]")).ToHaveAttributeAsync("href", App.Url("/de"));
        await Expect(page.Page.Locator("link[rel=alternate][hreflang=en]")).ToHaveAttributeAsync("href", App.Url("/en"));
        await Expect(page.Page.Locator("link[rel=alternate][hreflang=de]")).ToHaveAttributeAsync("href", App.Url("/de"));
        await Expect(page.Page.Locator("link[rel=alternate][hreflang=x-default]")).ToHaveAttributeAsync("href", App.Url("/en"));
        await Expect(page.Page.Locator("meta[name=description]")).ToHaveAttributeAsync("content", new System.Text.RegularExpressions.Regex("^Testo begleitet"));
    }

    [Theory]
    [InlineData("/en/no-such-page", "en", "Page not found – Testo", "Page not found", ColorScheme.Light)]
    [InlineData("/de/no-such-page", "de", "Seite nicht gefunden – Testo", "Seite nicht gefunden", ColorScheme.Dark)]
    [InlineData("/no-such-page", "en", "Page not found – Testo", "Page not found", ColorScheme.Dark)]
    public async Task UnknownAddress_ShowsTheNotFoundPageInTheRightLanguage(string path, string language, string title, string heading, ColorScheme scheme)
    {
        var page = await NewPageAsync(options => options.ColorScheme = scheme);

        var response = await page.Page.GotoAsync(App.Url(path));

        Assert.Equal(404, response?.Status);
        await Expect(page.Page).ToHaveTitleAsync(title);
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", language);
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync(heading);
        // A page that stands in for a missing one has no language versions of its own.
        await Expect(page.Page.Locator("link[rel=alternate]")).ToHaveCountAsync(0);
        await page.AssertAccessibleAsync();
        page.AssertNoProblems("HTTP 404: " + path, "status of 404");
    }

    [Theory]
    [InlineData("en-US", "en", "Something went wrong – Testo", "Something went wrong", "Request ID:")]
    [InlineData("de-DE", "de", "Etwas ist schiefgelaufen – Testo", "Etwas ist schiefgelaufen", "Anfrage-ID:")]
    public async Task ServerError_ShowsTheFriendlyErrorPageWithoutDetails(string locale, string language, string title, string heading, string requestIdLabel)
    {
        var page = await NewPageAsync(options => options.Locale = locale);

        var response = await page.Page.GotoAsync(App.Url(TestoWebApplication.FailingPath));

        Assert.Equal(500, response?.Status);
        await Expect(page.Page).ToHaveTitleAsync(title);
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", language);
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync(heading);
        await Expect(page.Page.Locator("main")).ToContainTextAsync(requestIdLabel);
        // Nothing about the failure itself may reach the visitor.
        await Expect(page.Page.Locator("body")).Not.ToContainTextAsync("Exception");
        await page.AssertAccessibleAsync();
        page.AssertNoProblems("HTTP 500: " + TestoWebApplication.FailingPath, "status of 500");
    }

    [Theory]
    [InlineData("/en")]
    [InlineData("/app")]
    [InlineData("/en/no-such-page")]
    public async Task Page_OnASmallPhone_NeedsNoHorizontalScrolling(string path)
    {
        // WCAG 1.4.10 Reflow: content fits a 320 px wide viewport.
        var page = await OpenAsync(path, options => options.ViewportSize = new ViewportSize { Width = 320, Height = 640 });
        await page.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var overflow = await page.Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

        Assert.Equal(0, overflow);
    }
}
