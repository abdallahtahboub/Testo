using Autestiq.E2E.Support;
using Autestiq.Platform;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// Described tests in a real browser (Milestone 5, part 2; ADR-0030): nothing can be described until the project's
/// AI permission is given, the permission says what is sent, and a site that is not proved is refused by the service.
/// </summary>
public class DescribedTestTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task Tests_AreDescribedOnlyOnceAiIsAllowed_AndAnUnprovedSiteIsRefused()
    {
        // An environment that is somebody's real site, never proved.
        var environments = await OpenSignedInAsync(Routes.Environments("SHOP"));
        await Expect(environments.Page.GetByTestId("add-environment")).ToBeEnabledAsync();
        await environments.Page.GetByTestId("environment-name").FillAsync("Staging");
        await environments.Page.GetByTestId("environment-origin").FillAsync("https://staging.northwind.example");
        await environments.Page.GetByTestId("add-environment").ClickAsync();
        await Expect(environments.Page.GetByTestId("environment-Staging")).ToBeVisibleAsync();
        environments.AssertNoProblems();

        var page = await OpenSignedInAsync(Routes.Tests("SHOP"));
        await Expect(page.Page.GetByTestId("allow-ai")).ToBeEnabledAsync();

        // Off by default: the notice says what would be sent, and there is no form to describe a test.
        var permission = page.Page.GetByTestId("ai-permission");
        await Expect(permission).ToContainTextAsync("AI planning is off for this project");
        await Expect(permission).ToContainTextAsync("Never sent:");
        await Expect(page.Page.GetByTestId("describe-test")).ToHaveCountAsync(0);

        // Allowing it needs the acknowledgement.
        await page.Page.GetByTestId("allow-ai").ClickAsync();
        await Expect(page.Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Tick the permission first");
        await Expect(page.Page.GetByTestId("describe-test")).ToHaveCountAsync(0);

        await page.Page.GetByTestId("acknowledge-ai").GetByRole(AriaRole.Checkbox).CheckAsync();
        await page.Page.GetByTestId("allow-ai").ClickAsync();
        await Expect(permission).ToContainTextAsync("AI planning was allowed for this project");

        // The site is not proved, so the service refuses before anything is explored or sent.
        await page.Page.GetByTestId("test-description").FillAsync("Add two cups to the basket and check the total");
        await page.Page.GetByTestId("describe-test").ClickAsync();
        await Expect(page.Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("prove it is yours");
        await Expect(page.Page.Locator(".t-empty-state")).ToContainTextAsync("No tests yet");

        // Withdrawn, the form goes away again.
        await page.Page.GetByTestId("withdraw-ai").ClickAsync();
        await Expect(permission).ToContainTextAsync("AI planning is off for this project");
        await Expect(page.Page.GetByTestId("describe-test")).ToHaveCountAsync(0);

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Tests_InGerman_AreInGerman()
    {
        var page = await OpenSignedInAsync(Routes.Tests("SHOP"), options => options.Locale = "de-DE");

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Tests");
        await Expect(page.Page.GetByTestId("ai-permission")).ToContainTextAsync("Die KI-Planung ist für dieses Projekt aus");

        page.AssertNoProblems();
    }
}
