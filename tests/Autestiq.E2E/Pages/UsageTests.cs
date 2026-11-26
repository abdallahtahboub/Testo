using System.Text.RegularExpressions;
using Autestiq.E2E.Support;
using Autestiq.Platform;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// Usage in a real browser (E1.3): the seeded organization's month against its plan, with what is left said in words;
/// a viewer, who may read the organization, sees it too.
/// </summary>
public partial class UsageTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task Owner_SeesTheMonthAgainstThePlan()
    {
        var page = await OpenSignedInAsync(Routes.Usage);

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Usage");
        await Expect(page.Page.GetByTestId("usage-minutes")).ToContainTextAsync(MinutesOf());
        await Expect(page.Page.GetByTestId("usage-plan")).ToContainTextAsync(", version ");
        await Expect(page.Page.GetByTestId("usage-state")).ToContainTextAsync("minutes");
        await Expect(page.Page.GetByTestId("usage-month")).ToContainTextAsync("(UTC)");
        page.AssertNoProblems();
    }

    [Fact]
    public async Task Viewer_SeesUsageToo()
    {
        var page = await NewPageAsync();
        await page.Page.GotoAsync(App.Url(Routes.SignIn));
        await page.Page.FillAsync("input[name='Input.Email']", "claudia@northwind.example");
        await page.Page.FillAsync("input[name='Input.Password']", AppFixture.SeededPassword);
        await page.Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.App) + "$"));

        await page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Usage", Exact = true }).ClickAsync();

        await Expect(page.Page).ToHaveURLAsync(new Regex(Regex.Escape(Routes.Usage) + "$"));
        await Expect(page.Page.GetByTestId("usage-minutes")).ToContainTextAsync(MinutesOf());
        page.AssertNoProblems();
    }

    [GeneratedRegex(@"^Browser minutes\s*[\d,]+ of [\d,]+$")]
    private static partial Regex MinutesOf();
}
