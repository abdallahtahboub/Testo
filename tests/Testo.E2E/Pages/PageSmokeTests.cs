using Microsoft.Playwright;
using Testo.E2E.Support;
using Testo.Platform;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// The smoke test every app page gets: it loads, has the right title and exactly one main
/// heading, the browser reports no problems, and the accessibility scan is clean in the
/// light and in the dark theme.
/// </summary>
/// <remarks>
/// The public website has its own, driven from the page catalogue so that a page cannot be added
/// without one: <c>PublicSiteSmokeTests</c>.
/// </remarks>
public class PageSmokeTests(AppFixture app) : BrowserTest(app)
{
    public static TheoryData<string, string, string, string, ColorScheme> Pages
    {
        get
        {
            var pages = new TheoryData<string, string, string, string, ColorScheme>();
            foreach (var scheme in new[] { ColorScheme.Light, ColorScheme.Dark })
            {
                // The public website is covered page by page, in both languages, by
                // PublicSiteSmokeTests. What is left here is the product behind the sign-in.
                pages.Add("/app", "en", "App preview – Testo", "App shell preview", scheme);
                pages.Add(Routes.Runs, "en", "Test runs – Testo", "Test runs", scheme);

                // A run that is not this organization's. The page still has a title and one
                // heading, because "no such run" is a page like any other.
                pages.Add(
                    Routes.Run(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                    "en",
                    "Run … – Testo",
                    "No such run",
                    scheme);
            }

            return pages;
        }
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Page_Loads_WithTitleHeadingAndNoProblems(string path, string language, string title, string heading, ColorScheme scheme)
    {
        // /app requires a session (D-1); the public pages must not.
        var page = path.StartsWith(Routes.App, StringComparison.Ordinal)
            ? await OpenSignedInAsync(path, options => options.ColorScheme = scheme)
            : await OpenAsync(path, options => options.ColorScheme = scheme);

        await Expect(page.Page).ToHaveTitleAsync(title);
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("lang", language);
        await Expect(page.Page.Locator("h1")).ToHaveCountAsync(1);
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync(heading);
        await Expect(page.Page.Locator("main#main-content")).ToHaveCountAsync(1);

        await page.AssertAccessibleAsync();
        page.AssertNoProblems();
    }
}
