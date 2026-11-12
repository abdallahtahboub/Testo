using Testo.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>The app area: interactive over a server connection, with the registered modules behind flags.</summary>
public class AppShellTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task AppPage_BecomesInteractiveOverAServerConnection()
    {
        var page = await OpenAsync("/app");

        await page.Page.WaitUntilInteractiveAsync();

        Assert.Contains(page.WebSockets, socket => socket.Contains("/_blazor", StringComparison.Ordinal));
        page.AssertNoProblems();
    }

    [Fact]
    public async Task FluentButton_Click_UpdatesThePage()
    {
        var page = await OpenAsync("/app");
        await page.Page.WaitUntilInteractiveAsync();
        var button = page.Page.GetByTestId("click-button");

        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 0");
        await button.ClickAsync();
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 1");
        await button.ClickAsync();
        await Expect(page.Page.GetByTestId("click-count")).ToHaveTextAsync("Clicks: 2");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task Modules_AreListedWithTheirFlagsSwitchedOff()
    {
        var page = await OpenAsync("/app");

        var rows = page.Page.GetByTestId("module-row");
        await Expect(rows).ToHaveCountAsync(7);
        await Expect(rows.Locator("th")).ToHaveTextAsync(["analysis", "defects", "design", "execution", "planning", "reporting", "settings"]);
        await Expect(rows.Locator("td:nth-child(2)")).ToHaveTextAsync(
            ["Module.Analysis", "Module.Defects", "Module.Design", "Module.Execution", "Module.Planning", "Module.Reporting", "Module.Settings"]);
        await Expect(rows.Locator("td:nth-child(3)")).ToHaveTextAsync(["Off", "Off", "Off", "Off", "Off", "Off", "Off"]);
    }

    [Fact]
    public async Task UnknownAppAddress_ShowsTheNotFoundPage()
    {
        var page = await NewPageAsync();

        var response = await page.Page.GotoAsync(App.Url("/app/no-such-page"));

        Assert.Equal(404, response?.Status);
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Page not found");
    }

    [Fact]
    public async Task WebsiteLink_LeadsBackToThePublicHomePage()
    {
        var page = await OpenAsync("/app");
        await page.Page.WaitUntilInteractiveAsync();

        await page.Page.GetByRole(Microsoft.Playwright.AriaRole.Link, new Microsoft.Playwright.PageGetByRoleOptions { Name = "Website", Exact = true }).ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(App.Url("/en"));
        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Quality you can see, from requirement to release");
        page.AssertNoProblems();
    }
}
