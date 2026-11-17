using System.Text.Json;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Testing;
using Autestiq.E2E.Support;
using Autestiq.Platform;
using Microsoft.Playwright;
using Npgsql;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// Described tests in a real browser (Milestone 5, part 2; ADR-0030): nothing can be described until the project's
/// AI permission is given, the permission says what is sent, and a site that is not proved is refused by the service.
/// </summary>
public class DescribedTestTests(AppFixture app) : BrowserTest(app)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly string[] Sent = ["Task: Add a cup and check the total\nPages seen: / Catalogue"];

    [Fact]
    public async Task Test_APlannedOne_KeepsWhatAutestiqCheckedApartFromTheAisUnverifiedNotes_AndShowsWhatWasSent()
    {
        var number = await PlannedTestAsync(App);

        // The list links to it; the test opens the link's address, as a person following it would.
        var list = await OpenSignedInAsync(Routes.Tests("SHOP"));
        var address = await list.Page.GetByTestId($"test-{number}").GetAttributeAsync("href");
        list.AssertNoProblems();

        var page = await OpenSignedInAsync(address ?? throw new InvalidOperationException("The test is not linked from the list."));

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync($"Test #{number}");
        await Expect(page.Page.GetByTestId("test-steps")).ToContainTextAsync("Check the text");
        await Expect(page.Page.GetByTestId("test-steps")).ToContainTextAsync("Basket total");
        await Expect(page.Page.GetByTestId("test-checks")).ToContainTextAsync("Every element the plan names was seen while exploring.");
        await Expect(page.Page.GetByRole(AriaRole.Heading, new() { Name = "The AI's notes: unverified" })).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("test-ai-notes")).ToContainTextAsync("I assumed the basket starts empty.");
        await Expect(page.Page.GetByTestId("test-python")).ToContainTextAsync("def test_");
        // What was sent sits folded under the model's name and the cost, one fold per attempt.
        await Expect(page.Page.GetByText("Exactly the text about your application that was sent to claude-sonnet-5-5")).ToContainTextAsync("$0.0123");
        await page.Page.GetByTestId("test-sent").GetByText("Attempt 1").ClickAsync();
        await Expect(page.Page.GetByTestId("test-sent")).ToContainTextAsync("Pages seen: / Catalogue");

        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "autestiq-test-page.png"), FullPage = true });
        page.AssertNoProblems();
        await page.AssertAccessibleAsync();
    }

    [Fact]
    public async Task Test_APlanThatMayChangeData_IsApprovedOnlyWithTheTick_AndARunOfAnUnprovedSiteIsRefused()
    {
        // Its second step clicks "add-cup", which nothing showed to be a link: it may change data.
        var number = await PlannedTestAsync(App);
        var list = await OpenSignedInAsync(Routes.Tests("SHOP"));
        var address = await list.Page.GetByTestId($"test-{number}").GetAttributeAsync("href");
        list.AssertNoProblems();

        var page = await OpenSignedInAsync(address ?? throw new InvalidOperationException("The test is not linked from the list."));
        await Expect(page.Page.GetByTestId("approve-plan")).ToBeEnabledAsync();

        await Expect(page.Page.GetByTestId("step-2").GetByTestId("may-change-data")).ToHaveTextAsync("May change data");
        await Expect(page.Page.GetByTestId("step-1").GetByTestId("may-change-data")).ToHaveCountAsync(0);
        await Expect(page.Page.GetByTestId("approval-changes")).ToContainTextAsync("Step 2 may change data");
        await Expect(page.Page.GetByTestId("runs-empty")).ToBeVisibleAsync();

        // Without the tick, nothing is approved.
        await page.Page.GetByTestId("approve-plan").ClickAsync();
        await Expect(page.Page.GetByTestId("approval").GetByRole(AriaRole.Alert)).ToContainTextAsync("This plan may change data");
        await Expect(page.Page.GetByTestId("run-test")).ToHaveCountAsync(0);

        await page.Page.GetByTestId("allow-change").GetByRole(AriaRole.Checkbox).CheckAsync();
        await page.Page.GetByTestId("approve-plan").ClickAsync();
        await Expect(page.Page.GetByTestId("approval-state")).ToContainTextAsync("Approved by Mara Lindqvist");
        await Expect(page.Page.GetByTestId("approval-state")).ToContainTextAsync("This approval allows the plan to change data.");

        // Approved, but the site is neither one of ours here nor proved: the service refuses the run.
        await page.Page.GetByTestId("run-test").ClickAsync();
        await Expect(page.Page.GetByTestId("approval").GetByRole(AriaRole.Alert)).ToContainTextAsync("prove it is yours");
        await Expect(page.Page.GetByTestId("runs-empty")).ToBeVisibleAsync();

        await page.Page.GetByTestId("withdraw-approval").ClickAsync();
        await Expect(page.Page.GetByTestId("approval-state")).ToContainTextAsync("Nothing runs until an owner or a QA lead approves this plan.");

        page.AssertNoProblems();
        await page.AssertAccessibleAsync();
    }

    /// <summary>
    /// A planned test, written straight into the database: planning needs a model, and the browser tests never call one.
    /// </summary>
    private static async Task<int> PlannedTestAsync(AppFixture app)
    {
        await using var connection = new NpgsqlConnection(app.ConnectionString);
        await connection.OpenAsync();

        await using var find = new NpgsqlCommand("SELECT organization_id, id FROM projects WHERE key_prefix = 'SHOP'", connection);
        Guid organization, project;
        await using (var reader = await find.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            (organization, project) = (reader.GetGuid(0), reader.GetGuid(1));
        }

        var environment = Guid.CreateVersion7();
        var testCase = Guid.CreateVersion7();
        var number = Random.Shared.Next(100, 100_000);
        var plan = new
        {
            Actions = new[]
            {
                new PlannedAction(ActionKind.Goto, "Open the catalogue", Path: "/"),
                new PlannedAction(ActionKind.Click, "Add a cup", ActionTarget.ByTestId("add-cup")),
                new PlannedAction(ActionKind.AssertText, "Basket total", ActionTarget.ByTestId("basket-total"), Text: "£11.25"),
            },
            Coverage = PlanCoverage.Full,
        };
        PlanNote[] notes =
        [
            new("Every element the plan names was seen while exploring.", PlanNoteSource.CheckedByAutestiq),
            new("I assumed the basket starts empty.", PlanNoteSource.Ai),
        ];

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO environments (id, organization_id, project_id, name, origin, kind, created_at, updated_at)
            VALUES (@environment, @organization, @project, @environmentName, @origin, 'Test', now(), now());
            INSERT INTO test_cases (id, organization_id, project_id, environment_id, number, description, status, plan, notes,
                sent_to_model, model, model_cost, planned_at, created_at, updated_at)
            VALUES (@testCase, @organization, @project, @environment, @number, 'Add a cup and check the total', 'Planned', @plan, @notes,
                @sent, 'claude-sonnet-5-5', 0.0123, now(), now(), now());
            """,
            connection);
        insert.Parameters.AddWithValue("environment", environment);
        insert.Parameters.AddWithValue("environmentName", $"Shop{number}");

        // An environment's address is unique in its project, and every test here adds one.
        insert.Parameters.AddWithValue("origin", $"http://127.0.0.1:{10_000 + number % 50_000}");
        insert.Parameters.AddWithValue("organization", organization);
        insert.Parameters.AddWithValue("project", project);
        insert.Parameters.AddWithValue("testCase", testCase);
        insert.Parameters.AddWithValue("number", number);
        insert.Parameters.AddWithValue("plan", JsonSerializer.Serialize(plan, Web));
        insert.Parameters.AddWithValue("notes", JsonSerializer.Serialize(notes, Web));
        insert.Parameters.AddWithValue("sent", JsonSerializer.Serialize(Sent, Web));
        await insert.ExecuteNonQueryAsync();

        return number;
    }

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
        await Expect(page.Page.GetByText("Add two cups to the basket and check the total")).ToHaveCountAsync(1);

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
