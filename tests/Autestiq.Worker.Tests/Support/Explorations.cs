using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// Explorations shared between tests. Pressing an application's controls takes tens of seconds, and
/// most tests ask a different question of the same exploration, so each way of exploring a target is
/// done once.
/// </summary>
internal static partial class Explorations
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<WorkerResult>>> Shared = new(StringComparer.Ordinal);

    /// <summary>What the benchmark gives an exploration: time for the presses, and pages for a larger application.</summary>
    public static RunBudget Budget { get; } = new(TimeSpan.FromMinutes(10), 100, 50 * 1024 * 1024, MaxPagesCrawled: 24);

    /// <summary>The exploration of this target done this way, made once and then remembered.</summary>
    public static async Task<WorkerResult> OnceAsync(
        string origin,
        bool pressing,
        bool forms,
        Func<Task>? startAfresh = null,
        string? named = null)
    {
        var shared = Shared.GetOrAdd(
            $"{named ?? origin}|{pressing}|{forms}",
            _ => new Lazy<Task<WorkerResult>>(() => Jobs.ExploreAsync(
                origin,
                Budget,
                exploreStates: pressing,
                submitForms: forms,
                startAfresh,
                named,

                // Not one test's to cancel: the others are waiting for it too.
                CancellationToken.None)));

        return await shared.Value.ConfigureAwait(false);
    }

    /// <summary>The snapshot of a result, which a finished exploration always has.</summary>
    public static SiteSnapshot SnapshotOf(WorkerResult result)
    {
        Assert.Equal(RunStatus.Passed, result.Status);
        return Assert.IsType<SiteSnapshot>(result.Snapshot);
    }

    /// <summary>Everything a snapshot says, as text, so that two can be held against each other.</summary>
    public static string Text(SiteSnapshot snapshot) => string.Join(
        "\n",
        snapshot.Pages.Select(page =>
            page.Path
            + " <- " + string.Join(" > ", page.ReachedBy ?? [])
            + " :: " + page.Title + " :: " + page.Heading + " :: "
            + string.Join(",", page.Elements.Select(element => $"{element.Role}/{element.TestId}/{element.Name}/{element.Text}"))));

    /// <summary>Every element of every page.</summary>
    public static IEnumerable<SnapshotElement> Elements(SiteSnapshot snapshot) =>
        snapshot.Pages.SelectMany(page => page.Elements);

    /// <summary>Whether any page carries an element with this test id.</summary>
    public static bool Shows(SiteSnapshot snapshot, string testId) =>
        Elements(snapshot).Any(element => element.TestId == testId);

    /// <summary>Whether any path the exploration took pressed this control.</summary>
    public static bool Pressed(SiteSnapshot snapshot, string testId) =>
        snapshot.Pages.Any(page => page.ReachedBy is { } steps
            && steps.Any(step => step.EndsWith("click " + testId, StringComparison.Ordinal)));

    /// <summary>How many visits the exploration says it made to press controls.</summary>
    public static int Visits(WorkerResult result)
    {
        var said = result.Steps[0].Message ?? "";
        var match = VisitsPattern().Match(said);

        Assert.True(match.Success, $"The exploration did not say how many visits it made: '{said}'.");

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"in (\d+) visit\(s\)")]
    private static partial Regex VisitsPattern();
}
