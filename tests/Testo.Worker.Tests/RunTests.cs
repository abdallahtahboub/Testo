using Testo.Contracts.Execution;
using Testo.DemoShop;
using Testo.Worker.Tests.Support;

// Testo.Worker.Scenarios is a namespace in scope here, which would otherwise shadow the catalogue.
using Catalogue = Testo.Contracts.Execution.Scenarios;

namespace Testo.Worker.Tests;

/// <summary>
/// The worker driving a real browser against a real target, which is the whole point of the
/// slice. No model chooses anything here (ADR-0018).
/// </summary>
[Collection(Serially.Name)]
public class RunTests(DemoShopFixture shop)
{
    [Fact]
    public async Task ARunThatPasses_ReportsEveryStepAndAScreenshotForEach()
    {
        var result = await RunAsync(Catalogue.DemoShopBasket, out var artifacts);

        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.Null(result.FailureReason);
        Assert.All(result.Steps, step => Assert.Equal(StepStatus.Passed, step.Status));

        // A passed step has nothing to say unless something went wrong collecting its evidence, so
        // this assertion is also the diagnosis: a missing screenshot names its own reason here.
        Assert.All(result.Steps, step => Assert.Null(step.Message));

        // The file, not just the name of one. A name pointing at nothing is not evidence.
        Assert.All(result.Steps, step =>
        {
            Assert.NotNull(step.ScreenshotFile);
            Assert.True(
                File.Exists(Path.Combine(artifacts, step.ScreenshotFile!)),
                $"step {step.Order} reported {step.ScreenshotFile} and there is no such file.");
        });

        // Something was actually fetched, which the budget counts and the record keeps.
        Assert.True(result.BytesDownloaded > 0);
    }

    [Fact]
    public async Task ARunThatFails_NamesTheStepAndKeepsTheScreenshotOfTheMoment()
    {
        // The shop is sound unless told otherwise, so the defect this scenario looks for is
        // switched on for the length of the test. A target that is broken by default would make
        // every other measurement against it suspect.
        using var defect = DemoShopDefects.On(Defects.CheckoutTotal);

        var result = await RunAsync(Catalogue.DemoShopCheckoutTotal, out var artifacts);

        Assert.Equal(RunStatus.Failed, result.Status);

        var failed = Assert.Single(result.Steps, step => step.Status == StepStatus.Failed);
        Assert.Equal("The checkout asks for the same amount", failed.Description);
        Assert.Contains("£53.25", failed.Message!, StringComparison.Ordinal);

        // The evidence a person acts on: the page at the moment it was wrong.
        Assert.NotNull(failed.ScreenshotFile);
        Assert.True(File.Exists(Path.Combine(artifacts, failed.ScreenshotFile!)));

        // Steps before it ran; there are none after it in this scenario.
        Assert.Equal(3, result.Steps.Count(step => step.Status == StepStatus.Passed));
    }

    [Fact]
    public async Task AFailingRun_IsFailed_NotAnInfrastructureError()
    {
        // The distinction the whole status list exists for. A defect in the application under
        // test is Failed; anything else would make the failure rate a lie about it.
        using var defect = DemoShopDefects.On(Defects.CheckoutTotal);

        var result = await RunAsync(Catalogue.DemoShopCheckoutTotal);

        Assert.NotEqual(RunStatus.InfrastructureError, result.Status);
        Assert.NotEqual(RunStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task APagesOwnFetchOfCloudMetadata_IsRefused()
    {
        // Not a navigation, so nothing a navigation-level check would see. It is refused because
        // the proxy is the only route out (docs/execution-threat-model.md §4.1).
        var result = await RunAsync(Catalogue.DemoShopEgressProbe);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task AScenarioWithNoSuchName_IsAnInfrastructureError()
    {
        var result = await RunAsync("no-such-scenario");

        Assert.Equal(RunStatus.InfrastructureError, result.Status);
        Assert.Contains("no scenario", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARunThatOutlastsItsWallClock_TimesOutRatherThanFailing()
    {
        // No time at all, so the deadline has passed before the first step. Deterministic, where
        // "a short budget and a fast machine" is a race. It must not be reported as a defect in
        // the application under test.
        var result = await RunAsync(
            Catalogue.DemoShopBasket,
            out _,
            RunBudget.Default with { WallClock = TimeSpan.Zero });

        Assert.Equal(RunStatus.TimedOut, result.Status);
        Assert.Contains("ran out of time", result.FailureReason!, StringComparison.Ordinal);

        // And nothing is claimed to have happened.
        Assert.All(result.Steps, step => Assert.Equal(StepStatus.Skipped, step.Status));
    }

    [Fact]
    public async Task ARunWithMoreStepsThanItsBudget_SaysSoAndStops()
    {
        var result = await RunAsync(
            Catalogue.DemoShopBasket,
            out _,
            RunBudget.Default with { MaxSteps = 2 });

        Assert.Equal(RunStatus.BudgetExceeded, result.Status);
        Assert.Contains("budget of 2", result.FailureReason!, StringComparison.Ordinal);

        // What it managed is still reported: two steps done, the rest untouched.
        Assert.Equal(2, result.Steps.Count(step => step.Status == StepStatus.Passed));
    }

    private Task<WorkerResult> RunAsync(string scenarioId) => RunAsync(scenarioId, out _);

    private Task<WorkerResult> RunAsync(string scenarioId, out string artifactDirectory) =>
        RunAsync(scenarioId, out artifactDirectory, RunBudget.Default);

    private Task<WorkerResult> RunAsync(string scenarioId, out string artifactDirectory, RunBudget budget)
    {
        var directory = Directory.CreateTempSubdirectory("testo-worker-test-");
        artifactDirectory = directory.FullName;

        var job = new WorkerJob(
            RunId: Guid.CreateVersion7(),
            scenarioId,
            shop.Origin,
            AllowedOrigins: [],

            // Ours, loopback, and named as trusted by the job. A customer-supplied target never is.
            TrustedLocalOrigins: [shop.Origin],
            budget,
            directory.FullName);

        return new ScenarioRunner(TimeProvider.System).RunAsync(job, TestContext.Current.CancellationToken);
    }
}
