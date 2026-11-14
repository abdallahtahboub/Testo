using System.Diagnostics;
using Microsoft.Playwright;
using Testo.Contracts.Execution;
using Testo.Worker.Egress;
using Testo.Worker.Scenarios;

namespace Testo.Worker;

/// <summary>
/// Runs one scenario in one browser, inside the budgets the job set, and reports what happened.
/// </summary>
/// <remarks>
/// Every way of stopping has its own outcome. A step that fails is <see cref="RunStatus.Failed"/>
/// and says what the application did; running out of time is <see cref="RunStatus.TimedOut"/>; a
/// browser that will not start is <see cref="RunStatus.InfrastructureError"/>, because nothing is
/// then known about the application under test and calling it a failure would be a lie about it.
/// </remarks>
public sealed class ScenarioRunner(TimeProvider timeProvider)
{
    /// <summary>Where the browser is, when the container puts it somewhere Playwright would not look.</summary>
    public const string BrowserPathVariable = "TESTO_BROWSER_PATH";

    /// <summary>Runs the job and returns its result. Never throws for an ordinary failure.</summary>
    public async Task<WorkerResult> RunAsync(WorkerJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var startedAt = timeProvider.GetUtcNow();
        var scenario = DemoShopScenarios.Find(job.ScenarioId);

        if (scenario is null)
        {
            return Infrastructure(job, startedAt, $"There is no scenario called '{job.ScenarioId}'.");
        }

        var refusals = new List<string>();
        await using var proxy = EgressProxy.Start(
            new EgressPolicy(job.AllowedOrigins, job.TrustedLocalOrigins),
            refusals.Add);

        // The budget is the run's, not a step's: a scenario that spends it all on one slow page
        // has still spent it.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(job.Budget.WallClock);

        var steps = scenario.Steps
            .Select((step, index) => new WorkerStepResult(
                index + 1, step.Description, StepStatus.Pending, null, null, null, null))
            .ToList();

        try
        {
            return await DriveAsync(job, scenario, steps, proxy, refusals, startedAt, budget.Token, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PlaywrightException exception)
        {
            // The browser itself, not the application under test.
            return Infrastructure(job, startedAt, exception.Message, steps);
        }
    }

    private async Task<WorkerResult> DriveAsync(
        WorkerJob job,
        Scenario scenario,
        List<WorkerStepResult> steps,
        EgressProxy proxy,
        List<string> refusals,
        DateTimeOffset startedAt,
        CancellationToken budget,
        CancellationToken cancellation)
    {
        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            // Everything the browser does goes through our proxy, which is the only route out
            // (ADR-0018). The browser is not asked to be well behaved; it is unable to misbehave.
            Proxy = new Proxy { Server = $"http://127.0.0.1:{proxy.Port}" },

            // The container ships its own browser and says where it is. Unset, Playwright looks
            // where it installs them itself.
            ExecutablePath = Environment.GetEnvironmentVariable(BrowserPathVariable) is { Length: > 0 } path
                ? path
                : null,

            Args = ["--no-sandbox", "--disable-dev-shm-usage"],
        }).ConfigureAwait(false);

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = job.TargetOrigin,
            AcceptDownloads = false,
            IgnoreHTTPSErrors = false,
        }).ConfigureAwait(false);

        context.SetDefaultTimeout(15_000);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        for (var index = 0; index < scenario.Steps.Count; index++)
        {
            if (index >= job.Budget.MaxSteps)
            {
                return Finish(job, RunStatus.BudgetExceeded, startedAt, steps, proxy,
                    $"The scenario has more steps than the budget of {job.Budget.MaxSteps} allows.");
            }

            if (proxy.BytesDownloaded > job.Budget.MaxBytesDownloaded)
            {
                return Finish(job, RunStatus.BudgetExceeded, startedAt, steps, proxy,
                    $"The run fetched more than its budget of {job.Budget.MaxBytesDownloaded} bytes.");
            }

            var outcome = await RunStepAsync(scenario.Steps[index], index, page, job, steps, budget)
                .ConfigureAwait(false);

            if (outcome is { } ending)
            {
                Skip(steps, from: index + 1);

                var reason = ending == RunStatus.Failed
                    ? steps[index].Message
                    : Reason(ending, refusals);

                return Finish(job, ending, startedAt, steps, proxy, reason);
            }
        }

        return Finish(job, RunStatus.Passed, startedAt, steps, proxy, failureReason: null);
    }

    /// <summary>Runs one step. Returns <c>null</c> when it passed, or how the run ends.</summary>
    private async Task<RunStatus?> RunStepAsync(
        ScenarioStep step,
        int index,
        IPage page,
        WorkerJob job,
        List<WorkerStepResult> steps,
        CancellationToken budget)
    {
        var stepStarted = timeProvider.GetUtcNow();
        steps[index] = steps[index] with { Status = StepStatus.Running, StartedAt = stepStarted };

        try
        {
            await step.Run(page, budget).ConfigureAwait(false);

            steps[index] = steps[index] with
            {
                Status = StepStatus.Passed,
                FinishedAt = timeProvider.GetUtcNow(),
                ScreenshotFile = await CaptureAsync(page, job, index + 1).ConfigureAwait(false),
            };

            return null;
        }
        catch (OperationCanceledException)
        {
            steps[index] = steps[index] with
            {
                Status = StepStatus.Failed,
                FinishedAt = timeProvider.GetUtcNow(),
                Message = "The run was stopped before this step finished.",
                ScreenshotFile = await CaptureAsync(page, job, index + 1).ConfigureAwait(false),
            };

            return budget.IsCancellationRequested ? RunStatus.TimedOut : RunStatus.Cancelled;
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            // What the application under test did, or did not do. A screenshot of the moment is
            // the difference between a failure somebody can act on and a line in a log.
            steps[index] = steps[index] with
            {
                Status = StepStatus.Failed,
                FinishedAt = timeProvider.GetUtcNow(),
                Message = FirstLine(exception.Message),
                ScreenshotFile = await CaptureAsync(page, job, index + 1).ConfigureAwait(false),
            };

            return RunStatus.Failed;
        }
    }

    /// <summary>A screenshot at the end of a step, named by its number. Best effort.</summary>
    private static async Task<string?> CaptureAsync(IPage page, WorkerJob job, int stepNumber)
    {
        var file = $"step-{stepNumber:D3}.png";

        try
        {
            Directory.CreateDirectory(job.ArtifactDirectory);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(job.ArtifactDirectory, file),
            }).ConfigureAwait(false);

            return file;
        }
        catch (Exception exception) when (exception is PlaywrightException or IOException)
        {
            // A run that fails is more useful without its screenshot than not at all.
            return null;
        }
    }

    private static void Skip(List<WorkerStepResult> steps, int from)
    {
        for (var index = from; index < steps.Count; index++)
        {
            steps[index] = steps[index] with { Status = StepStatus.Skipped };
        }
    }

    /// <summary>What to tell a person, when the ending is not an ordinary failure.</summary>
    private static string? Reason(RunStatus ending, List<string> refusals)
    {
        var refused = refusals.Count > 0
            ? $" The browser was refused: {string.Join("; ", refusals.Distinct())}."
            : "";

        return ending switch
        {
            RunStatus.TimedOut => $"The run ran out of time.{refused}",
            RunStatus.Cancelled => "The run was stopped.",
            _ => refused.Length > 0 ? refused.Trim() : null,
        };
    }

    private WorkerResult Finish(
        WorkerJob job,
        RunStatus status,
        DateTimeOffset startedAt,
        List<WorkerStepResult> steps,
        EgressProxy proxy,
        string? failureReason) =>
        new(job.RunId, status, failureReason, startedAt, timeProvider.GetUtcNow(), steps, proxy.BytesDownloaded);

    private WorkerResult Infrastructure(
        WorkerJob job,
        DateTimeOffset startedAt,
        string reason,
        List<WorkerStepResult>? steps = null) =>
        new(
            job.RunId,
            RunStatus.InfrastructureError,
            reason,
            startedAt,
            timeProvider.GetUtcNow(),
            steps ?? [],
            0);

    private static string FirstLine(string message)
    {
        var line = message.Split('\n')[0].Trim();
        return line.Length > 500 ? line[..500] : line;
    }
}
