using System.Collections.Concurrent;
using Microsoft.Playwright;
using Autestiq.Contracts.Execution;

namespace Autestiq.Worker;

/// <summary>
/// Keeps a run that was approved as read-only from sending anything that could change data (ADR-0030, part 3).
/// </summary>
/// <remarks>
/// <para>
/// The approval is decided in the application from the plan; this is where it holds, in the browser itself, because
/// the egress proxy sees only an encrypted tunnel for an HTTPS site and cannot tell a GET from a POST. Every request
/// the page makes, its scripts included, passes through the context's routes, and anything but GET, HEAD and OPTIONS
/// is aborted before it leaves the browser. WebSockets are closed before they connect, and service workers, whose
/// requests the routes do not see, are blocked when the context is made.
/// </para>
/// <para>
/// What it cannot see: a GET that changes data, which HTTP says should not exist and some sites have anyway. That is
/// why a plan is judged as well as guarded, and why the guard is not the approval.
/// </para>
/// </remarks>
internal sealed class ReadOnlyGuard
{
    private readonly ConcurrentQueue<string> _blocked = new();

    /// <summary>Whether a job is to be guarded: a planned run approved as read-only.</summary>
    public static bool Applies(WorkerJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.ReadOnly && job.Task == WorkerTask.Planned;
    }

    /// <summary>Whether a request with this method only reads.</summary>
    public static bool Reads(string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase)
        || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
        || method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);

    /// <summary>What was blocked, as method and path, in the order it was asked for.</summary>
    public IReadOnlyCollection<string> Blocked => _blocked;

    /// <summary>Fits the guard to a browser context, before any page is opened in it.</summary>
    public async Task FitAsync(IBrowserContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.RouteAsync("**/*", async route =>
        {
            if (Reads(route.Request.Method))
            {
                await route.FallbackAsync().ConfigureAwait(false);
                return;
            }

            _blocked.Enqueue($"{route.Request.Method} {PathOf(route.Request.Url)}");
            await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
        }).ConfigureAwait(false);

        await context.RouteWebSocketAsync("**/*", socket =>
        {
            _blocked.Enqueue($"WebSocket {PathOf(socket.Url)}");
            _ = socket.CloseAsync();
        }).ConfigureAwait(false);
    }

    /// <summary>The failure, with what was blocked and why it matters.</summary>
    public string Explain(string? failure)
    {
        var what = string.Join(", ", _blocked.Distinct().Take(5));

        return $"This test was approved as one that does not change data, and the site sent a request that could, which was blocked: {what}. "
            + "The test may have failed because of that, so it has no verdict. "
            + (failure is { Length: > 0 } ? $"The step said: {failure}" : "");
    }

    // The path only: a query string can carry what a person typed.
    private static string PathOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : "(unreadable address)";
}
