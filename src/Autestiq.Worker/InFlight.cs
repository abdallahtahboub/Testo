using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace Autestiq.Worker;

/// <summary>
/// The requests a page has in flight, counted from Playwright's own request events, so that the explorer can wait for
/// a page's data to arrive without waiting for half a second of silence after it (SiteExplorer.SettleAsync).
/// </summary>
internal sealed class InFlight
{
    private static readonly ConditionalWeakTable<IPage, InFlight> Pages = new();

    private readonly ConcurrentDictionary<IRequest, byte> _pending = new();

    private InFlight(IPage page)
    {
        page.Request += (_, request) => _pending.TryAdd(request, 0);
        page.RequestFinished += (_, request) => _pending.TryRemove(request, out byte _);
        page.RequestFailed += (_, request) => _pending.TryRemove(request, out byte _);
    }

    /// <summary>Starts counting a page's requests. Requests it has already sent are not counted.</summary>
    public static void Watch(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        _ = Pages.GetValue(page, watched => new InFlight(watched));
    }

    /// <summary>How many requests the page has in flight; none for a page that is not watched.</summary>
    public static int Of(IPage page) => Pages.TryGetValue(page, out var watched) ? watched._pending.Count : 0;
}
