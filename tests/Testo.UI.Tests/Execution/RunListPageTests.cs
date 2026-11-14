using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Testo.Contracts.Common;
using Testo.Contracts.Execution;
using Testo.Modules.Execution.Pages;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Execution;

public class RunListPageTests : DesignSystemTestContext
{
    private readonly FakeExecutionService _execution = new();

    public RunListPageTests()
    {
        Services.AddSingleton<IExecutionService>(_execution);
    }

    [Fact]
    public void Render_WithNoRuns_SaysSoAndStillOffersTheScenarios()
    {
        var page = Render<RunListPage>();

        Assert.Contains("Nothing has run yet", page.Find(".t-empty-state").TextContent);
        Assert.NotNull(page.Find("[data-testid='start-demo-shop-basket']"));
    }

    [Fact]
    public void Render_WithRuns_ShowsEachOneWithItsStatus()
    {
        _execution.Runs = new PagedResult<TestRunSummary>(
            [Summary(4, RunStatus.Failed), Summary(3, RunStatus.Passed)],
            TotalCount: 2,
            Page: 1,
            PageSize: 25);

        var page = Render<RunListPage>();
        var rows = page.FindAll("[data-testid='runs'] tbody tr");

        Assert.Equal(2, rows.Count);
        Assert.Contains("Failed", rows[0].TextContent);
        Assert.Contains("Passed", rows[1].TextContent);
        Assert.Equal("/app/runs/" + Summary(4, RunStatus.Failed).RunId, page.Find("[data-testid='run-4']").GetAttribute("href"));
    }

    [Fact]
    public void Start_WhenItIsQueued_GoesToTheRunItQueued()
    {
        var queued = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _execution.StartAnswer = OperationResult.With(queued);

        var page = Render<RunListPage>();
        page.Find("[data-testid='start-demo-shop-basket']").Click();

        Assert.Contains("start:demo-shop-basket", _execution.Calls);
        Assert.EndsWith($"/app/runs/{queued}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_WhenTheRoleDoesNotAllowIt_SaysSoAndStaysPut()
    {
        _execution.StartAnswer = OperationResult.Failure<Guid>("Execution_Error_NotAllowed");

        var page = Render<RunListPage>();
        page.Find("[data-testid='start-demo-shop-basket']").Click();

        // The use case answers with a key; the page is what turns it into a sentence.
        var problem = page.Find("[role='alert']");
        Assert.Equal("Your role does not allow starting or stopping test runs.", problem.TextContent.Trim());
        Assert.DoesNotContain("/app/runs/", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Start_WithNoProject_SaysSoWithoutCallingTheService()
    {
        _execution.CurrentProject = null;

        var page = Render<RunListPage>();
        page.Find("[data-testid='start-demo-shop-basket']").Click();

        Assert.Equal("There is no project by that name.", page.Find("[role='alert']").TextContent.Trim());
        Assert.DoesNotContain(_execution.Calls, call => call.StartsWith("start:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_InGerman_UsesTheGermanTexts()
    {
        using var german = new CultureScope("de");

        var page = Render<RunListPage>();

        Assert.Equal("Testläufe", page.Find("h1").TextContent.Trim());
        Assert.Contains("Es ist noch nichts gelaufen", page.Find(".t-empty-state").TextContent);
    }

    private static TestRunSummary Summary(int number, RunStatus status) => new(
        Guid.Parse($"44444444-4444-4444-4444-44444444444{number}"),
        number,
        "demo-shop-basket",
        "Basket keeps its items",
        status,
        Attempt: 1,
        StartedBy: "someone",
        QueuedAt: new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
        StartedAt: null,
        FinishedAt: null);
}
