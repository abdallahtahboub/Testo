using System.Text.RegularExpressions;
using Autestiq.E2E.Support;
using Autestiq.Platform;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// The organization page in a real browser (E1.4): an owner sees the seeded organization and its plan, read-only; a
/// viewer is offered neither the navigation item nor the organization.
/// </summary>
public class OrganizationTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task Owner_SeesTheOrganizationAndItsPlan_ReadOnly()
    {
        var page = await OpenSignedInAsync(Routes.Organization);

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Organization");
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Organization", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("organization-name")).Not.ToBeEmptyAsync();
        await Expect(page.Page.GetByTestId("organization-plan-name")).ToContainTextAsync(", version ");
        await Expect(page.Page.GetByTestId("organization-read-only")).ToContainTextAsync("does not change them yet");
        await Expect(page.Page.Locator("main input, main select, main textarea")).ToHaveCountAsync(0);
        page.AssertNoProblems();
    }

    [Fact]
    public async Task Viewer_IsOfferedNeitherTheNavigationItemNorTheOrganization()
    {
        var page = await NewPageAsync();
        await page.Page.GotoAsync(App.Url(Routes.SignIn));
        await page.Page.FillAsync("input[name='Input.Email']", "claudia@northwind.example");
        await page.Page.FillAsync("input[name='Input.Password']", AppFixture.SeededPassword);
        await page.Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.App) + "$"));

        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Projects", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Organization", Exact = true })).ToHaveCountAsync(0);

        await page.Page.GotoAsync(App.Url(Routes.Organization));

        await Expect(page.Page.GetByTestId("organization-not-allowed")).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("organization-name")).ToHaveCountAsync(0);
        page.AssertNoProblems();
    }
}
