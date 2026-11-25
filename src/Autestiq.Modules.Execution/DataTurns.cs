namespace Autestiq.Modules.Execution;

/// <summary>
/// One data call at a time per circuit. A circuit has one database context, which takes one operation at a time; the
/// Blazor dispatcher runs event handlers and timer callbacks one at a time only up to their first await, so a page's
/// refresh timer and a person's click could otherwise reach the context together and end the circuit (CI on
/// 2026-11-25: "A second operation was started on this context instance"). Every call these pages and their panels
/// make goes through here.
/// </summary>
internal sealed class DataTurns : IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);

    /// <summary>Whether a call is under way, so a periodic refresh can skip a tick instead of queueing behind it.</summary>
    public bool Busy => _turn.CurrentCount == 0;

    /// <summary>Waits for the turn, makes the call, and gives the turn back.</summary>
    public async Task<T> TakeAsync<T>(Func<Task<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        await _turn.WaitAsync().ConfigureAwait(false);

        try
        {
            return await call().ConfigureAwait(false);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>Waits for the turn, makes a call with no answer, and gives the turn back.</summary>
    public Task TakeAsync(Func<Task> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        return TakeAsync(async () =>
        {
            await call().ConfigureAwait(false);
            return true;
        });
    }

    /// <inheritdoc />
    public void Dispose() => _turn.Dispose();
}
