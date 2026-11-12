using Microsoft.Playwright;

namespace Testo.E2E.Support;

/// <summary>Small helpers shared by the browser tests.</summary>
internal static class PageExtensions
{
    /// <summary>A button of the theme or language switch, found by its accessible name.</summary>
    public static ILocator SwitchButton(this IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });

    /// <summary>
    /// Waits until the page shows a dark (or a light) background and fails if it never does.
    /// It waits because a style change can take a moment to reach its final value.
    /// </summary>
    public static Task ExpectDarkAsync(this IPage page, bool dark = true) =>
        page.WaitForFunctionAsync(
            """
            expectDark => {
                const [red, green, blue] = getComputedStyle(document.body).backgroundColor.match(/\d+/g).map(Number);
                return ((red * 299 + green * 587 + blue * 114) / 1000 < 128) === expectDark;
            }
            """,
            dark,
            new PageWaitForFunctionOptions { Timeout = 5_000 });

    /// <summary>The value of the <c>data-theme</c> attribute of the document, or null.</summary>
    public static Task<string?> ThemeAttributeAsync(this IPage page) =>
        page.Locator("html").GetAttributeAsync("data-theme");

    /// <summary>The value of a cookie of the page's browser context, or null.</summary>
    public static async Task<BrowserContextCookiesResult?> CookieAsync(this IPage page, string name) =>
        (await page.Context.CookiesAsync()).FirstOrDefault(cookie => cookie.Name == name);

    /// <summary>The id of the element that has keyboard focus (empty when it has none).</summary>
    public static Task<string> FocusedIdAsync(this IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement?.id ?? ''");

    /// <summary>Waits until an app page is connected to the server and reacts to input.</summary>
    public static Task WaitUntilInteractiveAsync(this IPage page) =>
        Assertions.Expect(page.GetByTestId("render-state")).ToContainTextAsync(new System.Text.RegularExpressions.Regex("Interactive|Interaktiv"));
}
