using Autestiq.Contracts.Execution;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// Guarded pressing (ADR-0041): on a site exploring may not act on, controls are pressed while the browser lets only
/// reads leave it. What reached the server is counted on the server.
/// </summary>
public class GuardedPressingTests(GuardedSiteFixture site) : IClassFixture<GuardedSiteFixture>
{
    // An exploration takes the better part of a minute; each way of exploring the site is done once per test run.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<bool, Lazy<Task<WorkerResult>>> Done = new();

    private Task<WorkerResult> ExploreAsync(bool pressGuarded) => Done.GetOrAdd(pressGuarded, _ => new Lazy<Task<WorkerResult>>(() => Jobs.ExploreAsync(
        site.OriginByName,
        Explorations.Budget,
        exploreStates: true,

        // Asked for, as the dispatcher asks for it: the policy refuses it on a site that may not be acted on.
        submitForms: true,
        pressGuarded: pressGuarded,

        // Not one test's to cancel: the others wait for it too.
        cancellation: CancellationToken.None))).Value;

    [Fact]
    public async Task Guarded_PressesEveryRouteToAWrite_AndNoneReachesTheServer()
    {
        var result = await ExploreAsync(pressGuarded: true);

        Assert.Equal(RunStatus.Passed, result.Status);

        // It pressed: a guard that holds because nothing was tried proves nothing.
        Assert.InRange(Explorations.Visits(result), 8, 150);

        // The page's own polling, fetch with POST, PUT, PATCH and DELETE, a beacon, a WebSocket, a form sent by script, and the two forms'
        // own buttons: none of it arrived.
        Assert.Empty(site.Writes);
    }

    [Fact]
    public async Task Guarded_FindsWhatATabShows_AndSaysHowToReachIt()
    {
        var snapshot = Explorations.SnapshotOf(await ExploreAsync(pressGuarded: true));

        var reviews = Assert.Single(snapshot.Pages, page => page.Elements.Any(element => element.Text == "Rated 4.5 by 12 people"));

        Assert.NotNull(reviews.ReachedBy);
        Assert.Contains(reviews.ReachedBy, step => step.Contains("click the button \"Reviews\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Guarded_DoesNotDescribeAPressThatTriedToSendSomething()
    {
        var result = await ExploreAsync(pressGuarded: true);
        var text = Explorations.Text(Explorations.SnapshotOf(result));

        // What a blocked save showed is the block, not the application: it is not put to a planner.
        Assert.DoesNotContain("Could not save", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Pinged", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Found", text, StringComparison.Ordinal);

        Assert.Contains("every request that could change data blocked", result.Steps[0].Message, StringComparison.Ordinal);
        Assert.Contains("would have sent something and are not described", result.Steps[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guarded_NeverTypesIntoAForm()
    {
        var snapshot = Explorations.SnapshotOf(await ExploreAsync(pressGuarded: true));

        Assert.DoesNotContain(snapshot.Pages, page => page.ReachedBy is { } steps
            && steps.Any(step => step.Contains("type test values", StringComparison.Ordinal) || step.Contains("sign in", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task WithoutGuardedPressing_TheSiteIsOnlyRead_AsBefore()
    {
        var result = await ExploreAsync(pressGuarded: false);
        var snapshot = Explorations.SnapshotOf(result);

        Assert.DoesNotContain(snapshot.Pages, page => page.ReachedBy is { Count: > 0 });
        Assert.Contains("refused", result.Steps[0].Message, StringComparison.Ordinal);
        Assert.Empty(site.Writes);
    }

    [Fact]
    public async Task AnExplorationShortOfTime_StopsInTime_AndReturnsWhatItFound_SayingItIsCutShort()
    {
        // Ten seconds: enough to read the page, not to press everything on it. Out of time, an exploration used to end
        // with nothing at all (R9, Juice Shop); now it stops at three quarters of its budget and describes what it has.
        var result = await Jobs.ExploreAsync(
            site.OriginByName,
            Explorations.Budget with { WallClock = TimeSpan.FromSeconds(10) },
            exploreStates: true,
            pressGuarded: true);

        var snapshot = Explorations.SnapshotOf(result);

        Assert.NotEmpty(snapshot.Pages);
        Assert.True(snapshot.Truncated);
        Assert.Empty(site.Writes);
    }
}
