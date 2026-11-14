using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Testo.Contracts.Common;
using Testo.Contracts.Execution;
using Testo.Modules.Execution.Pages;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Execution;

public class RunPageTests : DesignSystemTestContext
{
    private static readonly Guid TheRun = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly FakeExecutionService _execution = new();

    public RunPageTests()
    {
        Services.AddSingleton<IExecutionService>(_execution);
    }

    [Fact]
    public void Render_ARunThatIsNotThere_SaysSoWithoutSayingWhy()
    {
        // Another organization's run and a run that never existed get the same answer.
        _execution.Run = null;

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));

        Assert.Equal("No such run", page.Find("h1").TextContent.Trim());
        Assert.Contains("belongs to another organization", page.Find("[data-testid='not-found']").TextContent);
    }

    [Fact]
    public void Render_AFailedRun_ShowsWhyItStoppedAndTheEvidence()
    {
        _execution.Run = FakeExecutionService.Finished(TheRun);

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));

        Assert.Contains("Why it stopped", page.Find(".run__failure").TextContent);
        Assert.Contains("the checkout charged 42.00", page.Find(".run__failure").TextContent);

        var screenshot = page.Find(".run__screenshot");
        Assert.Equal("Screenshot of step 1", screenshot.GetAttribute("alt"));
        Assert.Contains("token=", screenshot.GetAttribute("src"));

        // Steps carry their own timings, so a failure is replayable rather than just red.
        Assert.Contains("Open the shop", page.Find("[data-testid='step-1']").TextContent);
        Assert.Contains("2.0 s", page.Find("[data-testid='step-1']").TextContent);
    }

    [Fact]
    public void Render_AFinishedRun_OffersARerunAndNoStop()
    {
        _execution.Run = FakeExecutionService.Finished(TheRun);

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));

        Assert.NotNull(page.Find("[data-testid='rerun']"));
        Assert.Empty(page.FindAll("[data-testid='cancel']"));
    }

    [Fact]
    public void Render_ARunningRun_OffersAStopAndNoRerun()
    {
        _execution.Run = FakeExecutionService.Finished(TheRun, RunStatus.Running, failureReason: null);

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));

        Assert.NotNull(page.Find("[data-testid='cancel']"));
        Assert.Empty(page.FindAll("[data-testid='rerun']"));
        Assert.Empty(page.FindAll(".run__failure"));
    }

    [Fact]
    public void Rerun_WhenItIsQueued_GoesToTheNewRun()
    {
        var again = Guid.Parse("66666666-6666-6666-6666-666666666666");
        _execution.Run = FakeExecutionService.Finished(TheRun);
        _execution.RerunAnswer = OperationResult.With(again);

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));
        page.Find("[data-testid='rerun']").Click();

        Assert.Contains($"rerun:{TheRun}", _execution.Calls);
        Assert.EndsWith($"/app/runs/{again}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_WhenTheRunHasAlreadyFinished_SaysSo()
    {
        _execution.Run = FakeExecutionService.Finished(TheRun, RunStatus.Running, failureReason: null);
        _execution.CancelAnswer = OperationResult.Failure("Execution_Error_AlreadyFinished");

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));
        page.Find("[data-testid='cancel']").Click();

        Assert.Equal("That run has already finished.", page.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Render_AFailureReasonThatIsAResourceKey_IsShownAsASentence()
    {
        // The application answers in keys and the worker answers in sentences. Both have to read.
        _execution.Run = FakeExecutionService.Finished(TheRun, RunStatus.Cancelled, "Execution_Error_AlreadyFinished");

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));

        Assert.Contains("That run has already finished.", page.Find(".run__failure").TextContent);
    }

    [Fact]
    public void Render_InGerman_UsesTheGermanTexts()
    {
        using var german = new CultureScope("de");
        _execution.Run = FakeExecutionService.Finished(TheRun);

        var page = Render<RunPage>(parameters => parameters.Add(component => component.RunId, TheRun));

        Assert.Contains("Lauf 7", page.Find("h1").TextContent);
        Assert.Contains("Schritte", page.Find(".run__steps").TextContent);
    }
}
