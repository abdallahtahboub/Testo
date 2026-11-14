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
        var scenario = ScenarioFor(job);

        if (scenario is null)
        {
            return Infrastructure(
                job,
                startedAt,
                job.Task == WorkerTask.Planned
                    ? "The job says it carries a plan and carries none."
                    : $"There is no scenario called '{job.ScenarioId}'.");
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

    /// <summary>
    /// What this job is a run of: one of ours, or a plan somebody else made.
    /// </summary>
    /// <remarks>
    /// An explore job has no scenario at all — it is one step, "look at the application", and the
    /// result it cares about is the snapshot rather than a verdict.
    /// </remarks>
    private static Scenario? ScenarioFor(WorkerJob job) => job.Task switch
    {
        WorkerTask.Planned => job.Plan is { Count: > 0 } plan ? PlannedScenario.From(job, plan) : null,
        WorkerTask.Explore => ExploreScenario,
        _ => DemoShopScenarios.Find(job.ScenarioId),
    };

    /// <summary>
    /// A placeholder so an explore job has the shape of a run. Its step does nothing; the crawl
    /// happens in <see cref="DriveAsync"/>, which is where the browser is.
    /// </summary>
    private static Scenario ExploreScenario { get; } = new(
        "explore",
        "Look at the application",
        "Walks the target and describes what is on it, so a plan can be made against it.",
        [new ScenarioStep("Walk the application", (_, _) => Task.CompletedTask)]);

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
        var deadline = startedAt.Add(job.Budget.WallClock);

        if (job.Task == WorkerTask.Explore)
        {
            return await LookAsync(job, page, steps, proxy, startedAt, budget).ConfigureAwait(false);
        }

        for (var index = 0; index < scenario.Steps.Count; index++)
        {
            // Checked here as well as by the cancellation token. The token stops a step that is
            // already running; this stops a run whose steps each finished in time but which has
            // nevertheless spent its budget, and it reports it as a budget, not as a failure.
            if (timeProvider.GetUtcNow() >= deadline)
            {
                Skip(steps, from: index);
                return Finish(job, RunStatus.TimedOut, startedAt, steps, proxy, Reason(RunStatus.TimedOut, refusals));
            }

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

    /// <summary>
    /// Walks the application and describes it. No verdict: an explore job is not a test, so it
    /// passes when the crawl finished and reports what it found either way.
    /// </summary>
    private async Task<WorkerResult> LookAsync(
        WorkerJob job,
        IPage page,
        List<WorkerStepResult> steps,
        EgressProxy proxy,
        DateTimeOffset startedAt,
        CancellationToken budget)
    {
        var step = steps[0] with { Status = StepStatus.Running, StartedAt = timeProvider.GetUtcNow() };

        try
        {
            var snapshot = await SiteExplorer.ExploreAsync(page, job, budget).ConfigureAwait(false);

            steps[0] = step with
            {
                Status = StepStatus.Passed,
                FinishedAt = timeProvider.GetUtcNow(),
                Message = $"Described {snapshot.Pages.Count} page(s)"
                    + (snapshot.Truncated ? $", stopping at the budget of {job.Budget.MaxPagesCrawled}." : "."),
            };

            return Finish(job, RunStatus.Passed, startedAt, steps, proxy, failureReason: null) with
            {
                Snapshot = snapshot,
            };
        }
        catch (OperationCanceledException)
        {
            steps[0] = step with { Status = StepStatus.Failed, FinishedAt = timeProvider.GetUtcNow() };
            return Finish(job, RunStatus.TimedOut, startedAt, steps, proxy, "Looking at the application ran out of time.");
        }
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

            var evidence = await CaptureAsync(page, job, index + 1).ConfigureAwait(false);

            steps[index] = steps[index] with
            {
                Status = StepStatus.Passed,
                FinishedAt = timeProvider.GetUtcNow(),

                // A step that passed usually has nothing to say. When its screenshot could not be
                // taken it does: evidence that goes missing quietly is the same as no evidence.
                Message = evidence.Problem,
                ScreenshotFile = evidence.File,
            };

            return null;
        }
        catch (OperationCanceledException)
        {
            var evidence = await CaptureAsync(page, job, index + 1).ConfigureAwait(false);

            steps[index] = steps[index] with
            {
                Status = StepStatus.Failed,
                FinishedAt = timeProvider.GetUtcNow(),
                Message = Say("The run was stopped before this step finished.", evidence.Problem),
                ScreenshotFile = evidence.File,
            };

            return budget.IsCancellationRequested ? RunStatus.TimedOut : RunStatus.Cancelled;
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            // What the application under test did, or did not do. A screenshot of the moment is
            // the difference between a failure somebody can act on and a line in a log.
            var evidence = await CaptureAsync(page, job, index + 1).ConfigureAwait(false);

            steps[index] = steps[index] with
            {
                Status = StepStatus.Failed,
                FinishedAt = timeProvider.GetUtcNow(),
                Message = Say(FirstLine(exception.Message), evidence.Problem),
                ScreenshotFile = evidence.File,
            };

            return RunStatus.Failed;
        }
    }

    /// <summary>A screenshot, or the reason there is none.</summary>
    /// <param name="File">The file inside the job's artifact directory, or <c>null</c>.</param>
    /// <param name="Problem">Why there is no screenshot, when there is none.</param>
    private readonly record struct Evidence(string? File, string? Problem);

    /// <summary>
    /// A screenshot at the end of a step, named by its number.
    /// </summary>
    /// <remarks>
    /// A step that clicks something which submits a form leaves the page navigating, and a
    /// screenshot taken in that moment is refused by the browser. That is how a CI run lost the
    /// evidence of a step that had passed, so the page is given a moment to settle first and one
    /// retry after that. A run is still more useful without one screenshot than not at all, so a
    /// failure here never fails the run — but it is always reported, because evidence that
    /// disappears quietly is indistinguishable from evidence nobody collected.
    /// </remarks>
    private static async Task<Evidence> CaptureAsync(IPage page, WorkerJob job, int stepNumber)
    {
        var file = $"step-{stepNumber:D3}.png";

        try
        {
            Directory.CreateDirectory(job.ArtifactDirectory);
            return new Evidence(await ShootAsync(page, job, file).ConfigureAwait(false), null);
        }
        catch (Exception first) when (IsCaptureProblem(first))
        {
            try
            {
                return new Evidence(await ShootAsync(page, job, file).ConfigureAwait(false), null);
            }
            catch (Exception again) when (IsCaptureProblem(again))
            {
                return new Evidence(null, $"No screenshot of this step: {FirstLine(again.Message)}");
            }
        }
    }

    private static async Task<string> ShootAsync(IPage page, WorkerJob job, string file)
    {
        // Settling is best effort: a page that never finishes loading is still worth a picture.
        try
        {
            await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 5_000 })
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsCaptureProblem(exception))
        {
            // Nothing to do about it here; the screenshot below either works or is reported.
        }

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(job.ArtifactDirectory, file),

            // A password typed into the application under test is, by necessity, in that
            // application. It does not also have to be in evidence we keep for ninety days
            // (threat model §4.4). The browser paints over these before the picture is taken.
            Mask = [page.Locator("input[type=password]")],
        }).ConfigureAwait(false);

        return file;
    }

    private static bool IsCaptureProblem(Exception exception) =>
        exception is PlaywrightException or TimeoutException or IOException;

    /// <summary>Joins what happened with anything that went wrong collecting the evidence.</summary>
    private static string Say(string what, string? problem) =>
        problem is null ? what : $"{what} {problem}";

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
