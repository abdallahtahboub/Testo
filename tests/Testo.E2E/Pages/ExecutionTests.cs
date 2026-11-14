using Testo.E2E.Support;
using Testo.Platform;
using static Microsoft.Playwright.Assertions;

namespace Testo.E2E.Pages;

/// <summary>
/// The execution pages in a real browser: the run list offers the scenarios, and a run id that
/// is not this organization's says so without saying anything else.
/// </summary>
public class ExecutionTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task RunList_WithNothingRunYet_OffersEveryScenarioAndSaysItIsEmpty()
    {
        var page = await OpenSignedInAsync(Routes.Runs);

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Test runs");

        // One button per scripted scenario, each with a real name beside it.
        await Expect(page.Page.Locator("[data-testid^='start-']")).ToHaveCountAsync(3);
        await Expect(page.Page.Locator(".t-empty-state")).ToContainTextAsync("Nothing has run yet");

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Run_AnIdThatIsNotThisOrganizations_SaysNoSuchRun()
    {
        // The same answer a run that never existed gets. Nothing tells the visitor which it is.
        var page = await OpenSignedInAsync(Routes.Run(Guid.Parse("99999999-9999-9999-9999-999999999999")));

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("No such run");
        await Expect(page.Page.Locator("[data-testid='steps']")).ToHaveCountAsync(0);

        page.AssertNoProblems();
    }

    [Fact]
    public async Task RunList_InGerman_IsInGerman()
    {
        var page = await OpenSignedInAsync(Routes.Runs, options => options.Locale = "de-DE");

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Testläufe");

        page.AssertNoProblems();
    }
}
