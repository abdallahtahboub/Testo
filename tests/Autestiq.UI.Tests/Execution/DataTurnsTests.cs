using Autestiq.Modules.Execution;

namespace Autestiq.UI.Tests.Execution;

/// <summary>
/// One data call at a time per circuit: a page's refresh and a person's click must never reach the database context
/// together (CI on 2026-11-25 ended a circuit with "A second operation was started on this context instance").
/// </summary>
public class DataTurnsTests
{
    [Fact]
    public async Task TakeAsync_TwoCallsAtOnce_NeverOverlap_AndTheSecondWaitsForTheFirst()
    {
        using var turns = new DataTurns();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inside = 0;
        var most = 0;
        var order = new List<string>();

        async Task<string> Call(string name, Task gate)
        {
            most = Math.Max(most, Interlocked.Increment(ref inside));
            await gate;
            lock (order)
            {
                order.Add(name);
            }

            Interlocked.Decrement(ref inside);
            return name;
        }

        var first = turns.TakeAsync(() => Call("save", release.Task));
        var second = turns.TakeAsync(() => Call("refresh", Task.CompletedTask));

        Assert.True(turns.Busy);
        Assert.False(second.IsCompleted);

        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, most);
        Assert.Equal(["save", "refresh"], order);
        Assert.False(turns.Busy);
    }

    [Fact]
    public async Task TakeAsync_ACallThatThrows_GivesTheTurnBack()
    {
        using var turns = new DataTurns();

        await Assert.ThrowsAsync<InvalidOperationException>(() => turns.TakeAsync(() => throw new InvalidOperationException("refused")));

        Assert.False(turns.Busy);
        Assert.Equal(1, await turns.TakeAsync(() => Task.FromResult(1)));
    }
}
