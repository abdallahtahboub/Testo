using Microsoft.Playwright;
using Testo.E2E.Support;
using Testo.Platform;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// The smoke test every page gets: it loads, has the right title and exactly one main
/// heading, the browser reports no problems, and the accessibility scan is clean in the
/// light and in the dark theme.
/// </summary>
public class PageSmokeTests(AppFixture app) : BrowserTest(app)
{
    public static TheoryData<string, string, string, string, ColorScheme> Pages
    {
        get
        {
            var pages = new TheoryData<string, string, string, string, ColorScheme>();
            foreach (var scheme in new[] { ColorScheme.Light, ColorScheme.Dark })
            {
                pages.Add("/en", "en", "Testo – AI-assisted QA platform", "Quality you can see, from requirement to release", scheme);
                pages.Add("/de", "de", "Testo – KI-gestützte QA-Plattform", "Qualität, die man sieht – von der Anforderung bis zum Release", scheme);
                pages.Add("/app", "en", "App preview – Testo", "App shell preview", scheme);
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
