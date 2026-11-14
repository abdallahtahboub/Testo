using Microsoft.Playwright;
using Testo.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>Keyboard use: skip link, focus order and a visible focus ring (WCAG 2.1.1, 2.4.1, 2.4.3, 2.4.7).</summary>
public class KeyboardTests(AppFixture app) : BrowserTest(app)
{
    private static Task<string> FocusedNameAsync(IPage page) =>
        page.EvaluateAsync<string>(
            """
            () => {
                const element = document.activeElement;
                return (element.getAttribute('aria-label') ?? element.textContent).replace(/\s+/g, ' ').trim();
            }
            """);

    [Fact]
    public async Task PublicPage_FirstTab_ReachesTheSkipLink_WhichLeadsToTheMainContent()
    {
        var page = await OpenAsync("/en");

        await page.Page.Keyboard.PressAsync("Tab");

        var skipLink = page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Skip to main content" });
        await Expect(skipLink).ToBeFocusedAsync();
        await Expect(skipLink).ToBeInViewportAsync();

        await page.Page.Keyboard.PressAsync("Enter");

        Assert.Equal("main-content", await page.Page.FocusedIdAsync());
        Assert.Equal("/en", page.Path);
    }

    [Fact]
    public async Task PublicPage_TabOrder_FollowsTheVisualOrderOfTheHeader()
    {
        var page = await OpenAsync("/en");
        var stops = new List<string>();

        // Far enough to get through the brand, every menu section and the tools. The icons inside
        // the menu summaries are decorative, so each summary's name is its one word.
        for (var index = 0; index < 13; index++)
        {
            await page.Page.Keyboard.PressAsync("Tab");
            stops.Add(await FocusedNameAsync(page.Page));
        }

        Assert.Equal(
            [
                "Skip to main content",
                "Testo home page",
                "Product",
                "Solutions",
                "Resources",
                "Company",
                "Pricing",
                "EN English",
                "DE Deutsch",
                "Light",
                "Dark",
                "Same as system",
                "Sign in",
            ],
            stops);
    }

    [Fact]
    public async Task FocusedControl_ShowsAFocusRing()
    {
        var page = await OpenAsync("/en");
        await page.Page.Keyboard.PressAsync("Tab");
        await page.Page.Keyboard.PressAsync("Tab");

        var brand = page.Page.Locator("a.site-header__brand");
        await Expect(brand).ToBeFocusedAsync();
        await Expect(brand).ToHaveCSSAsync("outline-style", "solid");
        await Expect(brand).ToHaveCSSAsync("outline-width", "2px");
        await Expect(brand).ToHaveCSSAsync("outline-offset", "2px");
    }

    [Fact]
    public async Task App_SkipLink_MovesFocusToTheMainContentWithoutLosingTheSession()
    {
        var page = await OpenSignedInAsync("/app");
        await page.Page.WaitUntilInteractiveAsync();
        await page.Page.GetByTestId("click-button").ClickAsync();
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 1");

        var skipLink = page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Skip to main content" });
        await skipLink.FocusAsync();
        await page.Page.Keyboard.PressAsync("Enter");

        Assert.Equal("main-content", await page.Page.FocusedIdAsync());
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 1");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task App_AfterLoading_FocusStartsAtThePageTitle()
    {
        // Screen reader users land on the title of the page instead of the top of the document.
        var page = await OpenSignedInAsync("/app");
        await page.Page.WaitUntilInteractiveAsync();

        await Expect(page.Page.Locator("h1")).ToBeFocusedAsync();
    }
}
