using Microsoft.AspNetCore.Hosting;
using Microsoft.Playwright;
using Testo.E2E.Support;
using Testo.Platform;
using Testo.Platform.Features;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// The showcase is where every component is on one page, which makes it the one place an
/// accessibility scan covers the whole design system at once.
/// </summary>
public class ShowcaseTests(AppFixture app) : BrowserTest(app)
{
    /// <summary>
    /// The showcase is off in the test application, as it is in production, so these tests
    /// run against their own copy with the flag on.
    /// </summary>
    private sealed class ShowcaseApplication : TestoWebApplication
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting($"FeatureManagement:{FeatureFlags.Showcase}", "true");
        }
    }

    [Fact]
    public async Task Showcase_WhenTheFlagIsOff_SaysSoRatherThanFailing()
    {
        var page = await OpenAsync(Routes.Showcase);

        await Expect(page.Page.GetByText("The showcase is switched off here")).ToBeVisibleAsync();
        page.AssertNoProblems();
    }

    [Theory]
    [InlineData(ColorScheme.Light)]
    [InlineData(ColorScheme.Dark)]
    public async Task Showcase_WithTheFlagOn_ShowsEverySectionAndPassesAnAccessibilityScan(ColorScheme scheme)
    {
        await using var application = new ShowcaseApplication();
        application.UseKestrel(0);
        application.StartServer();
        var address = application.ServerAddress();

        var page = await NewPageAsync(options => options.ColorScheme = scheme);
        await page.Page.GotoAsync(address + Routes.Showcase.TrimStart('/'));

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Component showcase");
        foreach (var section in Sections)
        {
            await Expect(page.Page.Locator("#" + section)).ToBeAttachedAsync();
        }

        await page.AssertAccessibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task Showcase_Always_FitsAPhoneWithoutHorizontalScrolling()
    {
        await using var application = new ShowcaseApplication();
        application.UseKestrel(0);
        application.StartServer();

        var page = await NewPageAsync(options => options.ViewportSize = new ViewportSize { Width = 320, Height = 640 });
        await page.Page.GotoAsync(application.ServerAddress() + Routes.Showcase.TrimStart('/'));
        await Expect(page.Page.Locator("h1")).ToBeVisibleAsync();

        var overflow = await page.Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

        Assert.Equal(0, overflow);
    }

    [Fact]
    public async Task Showcase_Tabs_RespondToTheArrowKeys()
    {
        await using var application = new ShowcaseApplication();
        application.UseKestrel(0);
        application.StartServer();

        var page = await NewPageAsync();
        var circuit = page.Page.WaitForWebSocketAsync();
        await page.Page.GotoAsync(application.ServerAddress() + Routes.Showcase.TrimStart('/'));
        await circuit;
        await WaitUntilInteractiveAsync(page);

        var strip = page.Page.Locator("#states [role=tablist]");
        await Expect(strip).ToBeVisibleAsync();

        await strip.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions { Name = "Data" }).FocusAsync();
        await page.Page.Keyboard.PressAsync("ArrowRight");

        await Expect(strip.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions { Name = "Loading" }))
            .ToHaveAttributeAsync("aria-selected", "true");
        await Expect(page.Page.Locator("#states .t-skeleton").First).ToBeVisibleAsync();
        page.AssertNoProblems();
    }

    /// <summary>
    /// Waits until the page is connected to the server and responding, using the showcase's
    /// own click counter.
    /// </summary>
    /// <remarks>
    /// Until the circuit connects the page is only prerendered: a click reaches no handler,
    /// and every component is replaced when it does. Waiting for the WebSocket is not quite
    /// enough on its own, because the re-render follows it, so the click is repeated until
    /// the count actually moves. The exact count does not matter, only that it responded.
    /// </remarks>
    private static async Task WaitUntilInteractiveAsync(TestPage page)
    {
        var button = page.Page.GetByTestId("showcase-count");
        var count = page.Page.GetByTestId("showcase-clicks");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await button.ClickAsync();
            if (await count.TextContentAsync() != "Clicks: 0")
            {
                return;
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The showcase never became interactive: clicking the counter changed nothing.");
    }

    private static readonly string[] Sections = ["foundations", "buttons", "status", "forms", "states", "data", "navigation"];
}
