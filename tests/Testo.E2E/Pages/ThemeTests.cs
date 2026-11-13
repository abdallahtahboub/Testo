using Microsoft.Playwright;
using Testo.E2E.Support;
using Testo.UI.Shared.Theming;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>The theme switch: light, dark or the same as the system; stored in a cookie; never a flash.</summary>
public class ThemeTests(AppFixture app) : BrowserTest(app)
{
    [Theory]
    [InlineData(ColorScheme.Light, false)]
    [InlineData(ColorScheme.Dark, true)]
    public async Task FirstVisit_FollowsTheSystemSetting(ColorScheme system, bool expectDark)
    {
        var page = await OpenAsync("/en", options => options.ColorScheme = system);

        Assert.Null(await page.Page.ThemeAttributeAsync());
        await page.Page.ExpectDarkAsync(expectDark);
        await Expect(page.Page.SwitchButton("Same as system")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Null(await page.Page.CookieAsync(ThemeCookie.Name));
    }

    [Fact]
    public async Task ChoosingDark_AppliesAtOnce_AndIsRenderedByTheServerFromThenOn()
    {
        var page = await OpenAsync("/en");
        await page.Page.EvaluateAsync("() => { window.pageWasNotReloaded = true; }");

        await page.Page.SwitchButton("Dark").ClickAsync();

        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        await page.Page.ExpectDarkAsync();
        Assert.True(await page.Page.EvaluateAsync<bool>("() => window.pageWasNotReloaded === true"), "The page reloaded; the theme script did not handle the switch.");
        await Expect(page.Page.SwitchButton("Dark")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(page.Page.SwitchButton("Light")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(page.Page.SwitchButton("Same as system")).ToHaveAttributeAsync("aria-pressed", "false");

        var cookie = await page.Page.CookieAsync(ThemeCookie.Name);
        Assert.Equal("dark", cookie?.Value);
        Assert.Equal("/", cookie?.Path);
        Assert.Equal(SameSiteAttribute.Lax, cookie?.SameSite);

        // The next request: the server itself renders the dark theme, so nothing flashes.
        var response = await page.Page.ReloadAsync();
        Assert.Contains("data-theme=\"dark\"", await response!.TextAsync(), StringComparison.Ordinal);
        await Expect(page.Page.SwitchButton("Dark")).ToHaveAttributeAsync("aria-pressed", "true");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task ChoosingSystemAgain_RemovesTheCookie()
    {
        var page = await OpenAsync("/en", options => options.ColorScheme = ColorScheme.Dark);
        await page.Page.SwitchButton("Light").ClickAsync();
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
        await page.Page.ExpectDarkAsync(dark: false);

        await page.Page.SwitchButton("Same as system").ClickAsync();

        await Expect(page.Page.SwitchButton("Same as system")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Null(await page.Page.ThemeAttributeAsync());
        await page.Page.ExpectDarkAsync();
        Assert.Null(await page.Page.CookieAsync(ThemeCookie.Name));
    }

    [Fact]
    public async Task ChoosingDark_WithoutJavaScript_WorksThroughTheServer()
    {
        var page = await OpenAsync("/en", options => options.JavaScriptEnabled = false);

        await page.Page.SwitchButton("Dark").ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(App.Url("/en"));
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        await Expect(page.Page.SwitchButton("Dark")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Equal("dark", (await page.Page.CookieAsync(ThemeCookie.Name))?.Value);

        await page.Page.SwitchButton("Same as system").ClickAsync();

        await Expect(page.Page.SwitchButton("Same as system")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Null(await page.Page.ThemeAttributeAsync());
        Assert.Null(await page.Page.CookieAsync(ThemeCookie.Name));
    }

    [Fact]
    public async Task ChoosingDark_InTheApp_KeepsTheSessionAndThemesTheFluentComponents()
    {
        var page = await OpenSignedInAsync("/app");
        await page.Page.WaitUntilInteractiveAsync();
        await page.Page.GetByTestId("click-button").ClickAsync();
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 1");

        await page.Page.SwitchButton("Dark").ClickAsync();

        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        await page.Page.ExpectDarkAsync();
        // The count survived, so the page did not reload and the server session is intact.
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 1");
        await page.Page.GetByTestId("click-button").ClickAsync();
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 2");

        // The Fluent UI button uses our brand color of the dark theme (pointer away, so it is not hovered).
        await page.Page.Mouse.MoveAsync(0, 0);
        var brand = await page.Page.EvaluateAsync<string>(
            """
            () => {
                const probe = document.createElement('span');
                probe.style.backgroundColor = 'var(--color-brand)';
                document.body.append(probe);
                const color = getComputedStyle(probe).backgroundColor;
                probe.remove();
                return color;
            }
            """);
        await Expect(page.Page.GetByTestId("click-button")).ToHaveCSSAsync("background-color", brand);
        page.AssertNoProblems();
    }

    [Fact]
    public async Task DarkCookie_InTheApp_IsStillPressedAfterTheSessionBecomesInteractive()
    {
        // Signed in from the start, because the second half of this journey is an app page, which
        // needs a session (D-1). What is under test is unchanged: a choice made on a statically
        // rendered page travelling into the interactive one.
        var page = await OpenSignedInAsync("/en");
        await page.Page.SwitchButton("Dark").ClickAsync();
        await Expect(page.Page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");

        await page.Page.GotoAsync(App.Url("/app"));
        await page.Page.WaitUntilInteractiveAsync();

        // The choice travelled from the server-rendered page into the interactive session.
        await Expect(page.Page.SwitchButton("Dark")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(page.Page.SwitchButton("Same as system")).ToHaveAttributeAsync("aria-pressed", "false");
        await page.Page.ExpectDarkAsync();
    }
}
