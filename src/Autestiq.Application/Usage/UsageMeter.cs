using System.Globalization;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Usage;

namespace Autestiq.Application.Usage;

/// <summary>
/// What a run used, as usage events (Milestone 3.6). One place for the rules, so the ledger, the quota and a later
/// bill all agree.
/// </summary>
public static class UsageMeter
{
    /// <summary>The source written on the events the dispatcher records.</summary>
    public const string Source = "dispatcher";

    /// <summary>The month an instant belongs to, in UTC.</summary>
    public static string PeriodOf(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>
    /// Whether a run is billable: decision B of commercial readiness §9. It must have reached the customer's
    /// application, which is the proxy relaying something from the one origin the run may reach; and it must not
    /// have ended by our fault, which is an infrastructure error or an operator's kill switch. A failure of the
    /// application, the test or the customer's configuration is billable.
    /// </summary>
    public static bool IsBillable(WorkerResult result, bool stoppedByOperator)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.BytesDownloaded > 0 && result.Status != RunStatus.InfrastructureError && !stoppedByOperator;
    }

    /// <summary>Exact browser seconds, from the worker's own start and finish, rounded up to the next second.</summary>
    public static long BrowserSecondsOf(WorkerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var elapsed = result.FinishedAt - result.StartedAt;
        return elapsed <= TimeSpan.Zero ? 0 : (long)Math.Ceiling(elapsed.TotalSeconds);
    }

    /// <summary>A month's seconds as the minutes a plan counts: rounded up once, for the whole month.</summary>
    public static long MinutesFor(long seconds) => seconds <= 0 ? 0 : (seconds + 59) / 60;

    /// <summary>The events one run produces. Their keys name the run, so recording them again stores nothing new.</summary>
    public static IReadOnlyList<UsageEvent> ForRun(TestRun run, WorkerResult result, bool billable, long artifactBytes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(result);

        var events = new List<UsageEvent>();
        var period = PeriodOf(now);

        void Add(UsageCategory category, long quantity) => events.Add(new UsageEvent
        {
            OrganizationId = run.OrganizationId,
            ProjectId = run.ProjectId,
            RunId = run.Id,
            Category = category,
            Quantity = quantity,
            Billable = billable,
            Period = period,
            OccurredAt = now,
            IdempotencyKey = KeyFor(run.Id, category),
            Source = Source,
        });

        Add(UsageCategory.Run, 1);

        if (BrowserSecondsOf(result) is var seconds and > 0)
        {
            Add(UsageCategory.BrowserSeconds, seconds);
        }

        if (artifactBytes > 0)
        {
            Add(UsageCategory.ArtifactBytes, artifactBytes);
        }

        return events;
    }

    /// <summary>What makes a run's event of one category the same event however often it is recorded.</summary>
    public static string KeyFor(Guid runId, UsageCategory category) =>
        string.Create(CultureInfo.InvariantCulture, $"run:{runId:N}:{category}");
}
