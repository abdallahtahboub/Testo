using Autestiq.E2E.Support;
using Autestiq.Platform;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// Projects and environments in a real browser (Milestone 5, part 1): the seeded project is listed, the plan's
/// project limit is said in words, and an environment can be added and its proof asked for.
/// </summary>
public class ProjectTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task Projects_ListTheSeededProject_AndSayWhenThePlanAllowsNoMore()
    {
        var page = await OpenSignedInAsync(Routes.Projects);

        // The form is disabled until the circuit connects: typing before then would be lost.
        await Expect(page.Page.GetByTestId("create-project")).ToBeEnabledAsync();

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Projects");
        await Expect(page.Page.GetByTestId("project-SHOP")).ToHaveTextAsync("Northwind Shop");

        // The seeded organization is on Developer, which allows one project, and has one.
        await page.Page.GetByTestId("project-name").FillAsync("Clinic");
        await page.Page.GetByTestId("project-key").FillAsync("clinic");
        await page.Page.GetByTestId("create-project").ClickAsync();

        await Expect(page.Page.GetByRole(Microsoft.Playwright.AriaRole.Alert)).ToHaveTextAsync("Your plan does not allow another project.");

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Environments_AddOne_ThenAskForItsProof_ShowsWhatToPublish()
    {
        var page = await OpenSignedInAsync(Routes.Environments("SHOP"));
        await Expect(page.Page.GetByTestId("add-environment")).ToBeEnabledAsync();

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Environments");

        var name = $"Staging{Guid.CreateVersion7():N}"[..16];
        await page.Page.GetByTestId("environment-name").FillAsync(name);
        await page.Page.GetByTestId("environment-origin").FillAsync($"https://{name.ToLowerInvariant()}.northwind.example");
        await page.Page.GetByTestId("add-environment").ClickAsync();

        var environment = page.Page.GetByTestId($"environment-{name}");
        await Expect(environment.GetByTestId("proof-status")).ToHaveTextAsync("Not proved");

        // The form is emptied for the next one.
        await Expect(page.Page.GetByTestId("environment-name")).ToHaveValueAsync("");
        await Expect(page.Page.GetByTestId("environment-origin")).ToHaveValueAsync("");

        await environment.GetByTestId($"prove-{name}").ClickAsync();

        await Expect(environment.GetByTestId("proof-file")).ToHaveTextAsync($"https://{name.ToLowerInvariant()}.northwind.example/.well-known/autestiq-verification.txt");
        await Expect(environment.GetByTestId("proof-token")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^[A-Za-z0-9_-]{43}$"));
        await Expect(environment.GetByTestId("proof-status")).ToHaveTextAsync("Waiting for the file");

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Environments_AProductionSite_IsRefusedUntilAnOwnerAcknowledgesAndAuthorizesIt()
    {
        var page = await OpenSignedInAsync(Routes.Environments("SHOP"));
        await Expect(page.Page.GetByTestId("add-environment")).ToBeEnabledAsync();

        var name = $"Live{Guid.CreateVersion7():N}"[..12];
        await page.Page.GetByTestId("environment-name").FillAsync(name);
        await page.Page.GetByTestId("environment-origin").FillAsync($"https://{name.ToLowerInvariant()}.northwind.example");
        await page.Page.GetByTestId("environment-production").GetByRole(Microsoft.Playwright.AriaRole.Checkbox).CheckAsync();
        await page.Page.GetByTestId("add-environment").ClickAsync();

        var environment = page.Page.GetByTestId($"environment-{name}");
        await Expect(environment.GetByTestId("production-badge")).ToHaveTextAsync("Production");
        await Expect(environment.GetByTestId("production-state")).ToContainTextAsync("Tests are refused against this production site");

        // Without the acknowledgement the owner is told why, and nothing is authorized.
        await environment.GetByTestId($"authorize-{name}").ClickAsync();
        await Expect(page.Page.GetByRole(Microsoft.Playwright.AriaRole.Alert)).ToContainTextAsync("Tick the acknowledgement first");

        await environment.GetByTestId($"acknowledge-{name}").GetByRole(Microsoft.Playwright.AriaRole.Checkbox).CheckAsync();
        await environment.GetByTestId($"authorize-{name}").ClickAsync();
        await Expect(environment.GetByTestId("production-state")).ToContainTextAsync("An owner authorized tests against this production site");

        await environment.GetByTestId($"withdraw-{name}").ClickAsync();
        await Expect(environment.GetByTestId("production-state")).ToContainTextAsync("Tests are refused against this production site");

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Environments_OfAProjectThisOrganizationDoesNotHave_SaysNoSuchProject()
    {
        var page = await OpenSignedInAsync(Routes.Environments("NOPE"));

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("No such project");

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Projects_InGerman_AreInGerman()
    {
        var page = await OpenSignedInAsync(Routes.Projects, options => options.Locale = "de-DE");

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Projekte");

        page.AssertNoProblems();
    }
}
