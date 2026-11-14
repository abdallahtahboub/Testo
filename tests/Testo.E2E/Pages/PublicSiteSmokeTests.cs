using Microsoft.Playwright;
using Testo.E2E.Support;
using Testo.Platform;
using Testo.Platform.Localization;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// Every page of the public website, in both languages: it loads, it has a title and exactly one
/// heading, the browser reports no problems, and the accessibility scan is clean.
/// </summary>
/// <remarks>
/// <para>
/// The list comes from <see cref="PublicPages.All"/> rather than being written out here, which is
/// the point: a page added without a smoke test is not possible, because adding the page to the
/// catalogue is what gives it one. A page in the catalogue with no component fails too, as a 404
/// with no heading.
/// </para>
/// <para>
/// The scan runs in the light theme for every page, and the dark theme is covered separately for a
/// handful of pages that carry the surfaces the others reuse. Scanning fifty-eight pages twice
/// would double the slowest part of CI to re-check the same six components.
/// </para>
/// </remarks>
public class PublicSiteSmokeTests(AppFixture app) : BrowserTest(app)
{
    public static TheoryData<string, string> EveryPage
    {
        get
        {
            var pages = new TheoryData<string, string>();

            foreach (var page in PublicPages.All)
            {
                foreach (var culture in SupportedCultures.All)
                {
                    pages.Add(page, culture);
                }
            }

            return pages;
        }
    }

    /// <summary>
    /// The pages worth scanning in the dark theme as well: the ones whose surfaces the rest reuse.
    /// </summary>
    public static TheoryData<string> DarkThemePages =>
    [
        PublicPages.Home,
        PublicPages.Product,
        PublicPages.ProductCapabilityE2EWeb,
        PublicPages.ProductSecurity,
        PublicPages.Pricing,
        PublicPages.Contact,
        PublicPages.Imprint,
    ];

    [Theory]
    [MemberData(nameof(EveryPage))]
    public async Task PublicPage_Loads_WithOneHeadingAndNoProblems(string page, string culture)
    {
        var path = LocalizedPath.For(culture, page);
        var visit = await OpenAsync(path, options => options.Locale = culture);

        await Expect(visit.Page.Locator("html")).ToHaveAttributeAsync("lang", culture);

        // Exactly one h1: a page with two has no outline, and a page with none leaves a screen
        // reader to guess what it is looking at.
        await Expect(visit.Page.Locator("h1")).ToHaveCountAsync(1);
        await Expect(visit.Page.Locator("main#main-content")).ToHaveCountAsync(1);

        var title = await visit.Page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title), $"{path} has no title.");
        Assert.EndsWith("Testo", title, StringComparison.Ordinal);

        var description = visit.Page.Locator("head meta[name='description']");
        await Expect(description).ToHaveCountAsync(1);
        Assert.False(
            string.IsNullOrWhiteSpace(await description.GetAttributeAsync("content")),
            $"{path} has an empty description, which is what a search result would show.");

        await visit.AssertAccessibleAsync();
        visit.AssertNoProblems();
    }

    [Theory]
    [MemberData(nameof(DarkThemePages))]
    public async Task PublicPage_InTheDarkTheme_IsStillAccessible(string page)
    {
        var visit = await OpenAsync(
            LocalizedPath.For(SupportedCultures.Default, page),
            options => options.ColorScheme = ColorScheme.Dark);

        await visit.AssertAccessibleAsync();
        visit.AssertNoProblems();
    }

    [Theory]
    [MemberData(nameof(DarkThemePages))]
    public async Task PublicPage_OpensNoCircuit(string page)
    {
        // A public page is static SSR without the Blazor script (ADR-0003). If that regresses,
        // every visitor pays for a SignalR connection to read a marketing page, and the symptom is
        // a slow first load rather than anything that looks like a fault.
        var visit = await OpenAsync(LocalizedPath.For(SupportedCultures.Default, page));

        await visit.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.DoesNotContain(visit.Requests, request => request.Contains("blazor.web.js", StringComparison.Ordinal));
        Assert.DoesNotContain(visit.Requests, request => request.Contains("_blazor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryPageInTheMenu_LeadsSomewhereThatExists()
    {
        // The menu is data, and data can name a page nobody wrote. Following every link in the
        // header and the footer is the cheapest way to find that out.
        var visit = await OpenAsync(LocalizedPath.For(SupportedCultures.Default));

        var links = await visit.Page.Locator("header a[href^='/en'], footer a[href^='/en']").AllAsync();
        var targets = new List<string>();

        foreach (var link in links)
        {
            if (await link.GetAttributeAsync("href") is { Length: > 0 } href && !targets.Contains(href))
            {
                targets.Add(href);
            }
        }

        Assert.NotEmpty(targets);

        foreach (var target in targets)
        {
            var response = await visit.Page.GotoAsync(App.Url(target));

            Assert.True(
                response?.Status == 200,
                $"{target} is linked from the chrome and answered {response?.Status}.");
        }
    }
}
