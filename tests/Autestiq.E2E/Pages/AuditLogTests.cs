using Autestiq.E2E.Support;
using Autestiq.Platform;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// The audit log in a real browser (E1.2): an owner reads it and narrows it by action and person, a backwards range is
/// said in words, and a viewer is offered neither the navigation item nor the log.
/// </summary>
public class AuditLogTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task Owner_NarrowsTheLog_ByActionAndPerson()
    {
        var page = await OpenSignedInAsync(Routes.AuditLog);
        await Expect(page.Page.GetByTestId("filter-action")).ToBeEnabledAsync();

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Audit log");
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Audit log", Exact = true })).ToBeVisibleAsync();

        // Signing in for the test run wrote this entry, so it is among the choices.
        await page.Page.GetByTestId("filter-action").SelectOptionAsync("user.signed_in");
        await page.Page.GetByTestId("filter-person").SelectOptionAsync(AppFixture.OwnerEmail);

        // Once both filters have been applied, every row is the owner signing in.
        var rows = page.Page.GetByTestId("audit-entry");
        await Expect(rows.First).ToBeVisibleAsync();
        await Expect(rows.Locator("td:nth-child(3)").Filter(new LocatorFilterOptions { HasNotText = "user.signed_in" })).ToHaveCountAsync(0);
        await Expect(rows.Locator("td:nth-child(2)").Filter(new LocatorFilterOptions { HasNotText = AppFixture.OwnerEmail })).ToHaveCountAsync(0);

        await page.Page.GetByTestId("filter-clear").ClickAsync();
        await Expect(page.Page.GetByTestId("filter-action")).ToHaveValueAsync("");

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Owner_ABackwardsRange_IsSaidInWords()
    {
        var page = await OpenSignedInAsync(Routes.AuditLog);
        await Expect(page.Page.GetByTestId("filter-from")).ToBeEnabledAsync();

        await page.Page.GetByTestId("filter-from").FillAsync("2026-11-22");
        await page.Page.GetByTestId("filter-from").DispatchEventAsync("change");
        await page.Page.GetByTestId("filter-to").FillAsync("2026-11-20");
        await page.Page.GetByTestId("filter-to").DispatchEventAsync("change");

        await Expect(page.Page.GetByTestId("audit-dates-problem")).ToContainTextAsync("before the first");
        await Expect(page.Page.GetByTestId("audit-empty")).ToBeVisibleAsync();
        page.AssertNoProblems();
    }

    [Fact]
    public async Task Viewer_IsOfferedNeitherTheNavigationItemNorTheLog()
    {
        var page = await NewPageAsync();
        await page.Page.GotoAsync(App.Url(Routes.SignIn));
        await page.Page.FillAsync("input[name='Input.Email']", "claudia@northwind.example");
        await page.Page.FillAsync("input[name='Input.Password']", AppFixture.SeededPassword);
        await page.Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(Routes.App) + "$"));

        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Projects", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Audit log", Exact = true })).ToHaveCountAsync(0);

        await page.Page.GotoAsync(App.Url(Routes.AuditLog));

        await Expect(page.Page.GetByTestId("audit-not-allowed")).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("audit-entries")).ToHaveCountAsync(0);
        await Expect(page.Page.GetByTestId("filter-person")).ToHaveCountAsync(0);
        page.AssertNoProblems();
    }
}
