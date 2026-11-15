using Microsoft.Playwright;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Worker;

/// <summary>
/// Walks the target and describes what is on it, so that a plan can be made against an application
/// as it actually is rather than as somebody imagined it.
/// </summary>
/// <remarks>
/// <para>
/// No model is involved here, and that is the point (ADR-0019 §5). The crawl is a breadth-first
/// walk of in-origin links from the start page, within a budget, so the same application produces
/// the same description — which is what makes a plan reproducible and a flake rate meaningful.
/// When the job asks for it (ADR-0021), it also presses the application's own controls and says what
/// each path reaches, by the same fixed, bounded rule.
/// A model choosing where to look could be led somewhere by the page it is reading.
/// </para>
/// <para>
/// Everything it returns is untrusted input. It is kept to roles, accessible names, test ids and
/// trimmed text: no markup, no attributes, no URLs, nothing that reads as code.
/// </para>
/// </remarks>
internal static partial class SiteExplorer
{
    /// <summary>How many elements of one page are described. A page with more says so by being cut.</summary>
    private const int MaxElementsPerPage = 60;

    /// <summary>How long a name or a piece of text may be before it is trimmed.</summary>
    private const int MaxTextLength = 120;

    /// <summary>How many links of one page are followed. A navigation of a thousand items is a page, not a site.</summary>
    private const int MaxLinksPerPage = 40;

    /// <summary>How many paths are taken from a sitemap. A large site lists thousands.</summary>
    private const int MaxSitemapPaths = 40;

    /// <summary>How many click paths are tried, in all. Each one is a fresh visit, so this is what exploring costs.</summary>
    private const int MaxReplays = 300;

    /// <summary>How many actions one path may take. Three reaches a basket with two products: add one, back to the catalogue, add another.</summary>
    private const int MaxPathDepth = 3;

    /// <summary>How many controls of one state are pressed. The rest are described, not exercised.</summary>
    private const int MaxControlsPerState = 8;

    /// <summary>How many distinct states exploration may add to a snapshot.</summary>
    private const int MaxExploredStates = 60;

    /// <summary>The roles worth describing: the things a plan can act on or check.</summary>
    private static readonly string[] Roles =
        ["button", "link", "textbox", "checkbox", "radio", "combobox", "heading"];

    /// <summary>
    /// Describes the target, following in-origin links until the page budget is spent.
    /// </summary>
    public static async Task<SiteSnapshot> ExploreAsync(
        IPage page,
        WorkerJob job,
        CancellationToken cancellationToken)
    {
        var origin = new Uri(job.TargetOrigin);
        var queue = new Queue<string>(["/"]);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/" };

        // Pages the application itself advertises. Following links alone is not enough: the demo
        // shop hides its checkout behind a basket that has something in it, so a crawl that starts
        // with an empty basket never reaches /checkout, /order/placed or /account — and six of the
        // first benchmark's twelve scenarios were about pages the planner had never been shown.
        // A real application publishes a sitemap for exactly this reason.
        foreach (var advertised in await SitemapPathsAsync(page, origin).ConfigureAwait(false))
        {
            if (seen.Add(advertised))
            {
                queue.Enqueue(advertised);
            }
        }
        var pages = new List<PageSnapshot>();
        var truncated = false;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pages.Count >= job.Budget.MaxPagesCrawled)
            {
                // Said rather than hidden: a planner that does not know it is looking at part of an
                // application will plan as though it were all of it.
                truncated = true;
                break;
            }

            var path = queue.Dequeue();

            var response = await Open(page, path).ConfigureAwait(false);

            if (response is null)
            {
                continue;
            }

            pages.Add(await DescribeAsync(page, path).ConfigureAwait(false));

            foreach (var link in await LinksAsync(page, origin).ConfigureAwait(false))
            {
                if (seen.Add(link))
                {
                    queue.Enqueue(link);
                }
            }
        }

        if (job.ExploreStates)
        {
            var explored = await ExploreStatesAsync(page, job, pages, cancellationToken).ConfigureAwait(false);
            pages.AddRange(explored.Pages);
            truncated |= explored.Truncated;
        }

        return new SiteSnapshot(job.TargetOrigin, pages, truncated);
    }

    /// <summary>
    /// Presses the application's own controls, one path at a time, and describes the state each path
    /// reaches. This is what lets a plan know that "add to basket" leads to the basket, and what the
    /// basket then holds, before the plan is written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every path starts from a fresh visit: cookies are cleared and the start page is loaded again,
    /// so no path inherits what an earlier one put in the basket. The order is breadth first, the
    /// paths are fixed by the application, and the number of visits is capped, so the same
    /// application is explored the same way every time. A time limit would not be deterministic.
    /// </para>
    /// <para>
    /// Only controls with a test id are pressed, and never one whose name says it changes something
    /// that should not change on the way (<see cref="ConsequentialPattern"/>): ordering, paying,
    /// deleting, signing out. A crawl exists to find states, not to buy things.
    /// </para>
    /// </remarks>
    private static async Task<(List<PageSnapshot> Pages, bool Truncated)> ExploreStatesAsync(
        IPage page,
        WorkerJob job,
        IReadOnlyList<PageSnapshot> crawled,
        CancellationToken cancellationToken)
    {
        var start = crawled.FirstOrDefault(candidate => candidate.Path == "/" && (candidate.ReachedBy is null or { Count: 0 }));

        if (start is null)
        {
            return ([], false);
        }

        // What is already in the snapshot. Only used to keep the output free of repeats.
        var known = new HashSet<string>(crawled.Select(Signature), StringComparer.Ordinal);
        var found = new List<PageSnapshot>();
        var queue = new Queue<(List<string> Path, PageSnapshot State)>();
        queue.Enqueue((new List<string>(), start));
        var replays = 0;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (path, state) = queue.Dequeue();

            if (path.Count >= MaxPathDepth)
            {
                continue;
            }

            foreach (var control in Clickable(state).Take(MaxControlsPerState))
            {
                if (replays >= MaxReplays || found.Count >= MaxExploredStates)
                {
                    // Said rather than hidden, for the same reason as the crawl's own budget.
                    return (found, true);
                }

                replays++;

                var reached = new List<string>(path) { control.TestId! };
                var next = await ReplayAsync(page, job, reached, cancellationToken).ConfigureAwait(false);

                if (next is null)
                {
                    continue;
                }

                var described = next with { ReachedBy = reached.Select(testId => "click " + testId).ToList() };

                // Every path is followed, even to a page that looks like one already found. The
                // catalogue reads the same whether the basket holds a teapot or a cup, and the cup
                // is only reachable through the teapot's path. Only the snapshot is kept free of repeats.
                if (known.Add(Signature(described)))
                {
                    found.Add(described);
                }

                queue.Enqueue((reached, described));
            }
        }

        return (found, false);
    }

    /// <summary>The controls of one state that exploration may press.</summary>
    private static IEnumerable<SnapshotElement> Clickable(PageSnapshot state) =>
        state.Elements.Where(element =>
            element.TestId is { Length: > 0 }
            && element.Role is "button" or "link"
            && !ConsequentialPattern().IsMatch(element.TestId + " " + element.Name));

    /// <summary>
    /// Visits the start page afresh, presses each control in turn, and describes where that ends.
    /// Null when the path cannot be carried out or leaves the target.
    /// </summary>
    private static async Task<PageSnapshot?> ReplayAsync(
        IPage page,
        WorkerJob job,
        IReadOnlyList<string> path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await page.Context.ClearCookiesAsync().ConfigureAwait(false);

        if (await Open(page, "/").ConfigureAwait(false) is null)
        {
            return null;
        }

        foreach (var testId in path)
        {
            // By test id, through Playwright's own escaping: the id came from the application, and
            // is never put into a selector as text.
            var target = page.GetByTestId(testId);

            try
            {
                if (await target.CountAsync().ConfigureAwait(false) != 1)
                {
                    return null;
                }

                await target.ClickAsync().ConfigureAwait(false);
                await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
            {
                return null;
            }
        }

        var here = new Uri(page.Url);
        var origin = new Uri(job.TargetOrigin);

        if (!string.Equals(here.GetLeftPart(UriPartial.Authority), origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await DescribeAsync(page, here.AbsolutePath).ConfigureAwait(false);
    }

    /// <summary>What makes two states the same: where they are and what is on them.</summary>
    private static string Signature(PageSnapshot state) =>
        state.Path + "|" + string.Join(',', state.Elements.Select(element => element.TestId ?? element.Name));

    /// <summary>
    /// The paths the application lists in its own <c>sitemap.xml</c>, if it has one.
    /// </summary>
    /// <remarks>
    /// Read as text and matched for location elements rather than parsed as XML, because this is
    /// untrusted input from the application under test and the only thing wanted from it is a list
    /// of paths on the same origin. Anything else in the file is ignored.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> SitemapPathsAsync(IPage page, Uri origin)
    {
        string text;

        try
        {
            // Through the browser, not through Playwright's own request context: that context
            // honours the ambient HTTP_PROXY of whatever machine the worker runs on, which is not
            // the run's egress proxy and answered 403 without the request ever reaching the
            // application. Every connection a run makes goes through the proxy ADR-0018 gives it,
            // and the browser is what that proxy is attached to.
            var response = await page.GotoAsync(
                new Uri(origin, "/sitemap.xml").AbsoluteUri,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);

            if (response is null || !response.Ok)
            {
                return [];
            }

            text = await response.TextAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            // An application without a sitemap is the normal case, not a fault.
            return [];
        }

        var paths = new List<string>();

        foreach (var match in LocationPattern().Matches(text).Cast<System.Text.RegularExpressions.Match>())
        {
            if (paths.Count >= MaxSitemapPaths)
            {
                break;
            }

            if (Uri.TryCreate(match.Groups[1].Value.Trim(), UriKind.Absolute, out var absolute)
                && string.Equals(
                    absolute.GetLeftPart(UriPartial.Authority),
                    origin.GetLeftPart(UriPartial.Authority),
                    StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(absolute.AbsolutePath);
            }
        }

        return paths;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<loc>\s*([^<]+?)\s*</loc>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex LocationPattern();

    [System.Text.RegularExpressions.GeneratedRegex(
        @"order|pay|purchase|place|delete|sign-?out|log-?out|submit|confirm|send",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex ConsequentialPattern();

    /// <summary>
    /// Opens a path, or gives up on it. A page that will not load is not a reason to abandon the
    /// crawl: the rest of the application is still worth describing.
    /// </summary>
    private static async Task<IResponse?> Open(IPage page, string path)
    {
        try
        {
            return await page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return null;
        }
    }

    private static async Task<PageSnapshot> DescribeAsync(IPage page, string path)
    {
        var title = await page.TitleAsync().ConfigureAwait(false);
        var heading = await FirstTextAsync(page.Locator("h1")).ConfigureAwait(false);

        var elements = new List<SnapshotElement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in Roles)
        {
            var found = page.Locator($"[role='{role}'], {HtmlFor(role)}");
            var count = Math.Min(await found.CountAsync().ConfigureAwait(false), MaxElementsPerPage);

            for (var index = 0; index < count && elements.Count < MaxElementsPerPage; index++)
            {
                var element = found.Nth(index);

                var name = Trim(await TextOrAttributeAsync(element).ConfigureAwait(false));
                var testId = await AttributeAsync(element, "data-testid").ConfigureAwait(false);

                if (name is null && testId is null)
                {
                    // Nothing a plan could name it by, so nothing worth describing.
                    continue;
                }

                if (testId is not null && !seen.Add(testId))
                {
                    continue;
                }

                elements.Add(new SnapshotElement(role, name ?? testId ?? "", testId));
            }
        }

        await AddTestIdElementsAsync(page, elements, seen).ConfigureAwait(false);

        return new PageSnapshot(path, Trim(title) ?? "", heading, elements);
    }

    /// <summary>
    /// Everything carrying a <c>data-testid</c>, whatever its role, with what it reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this the crawl collected only the interactive roles, and the first real measurement
    /// showed what that costs: every value these tests are about — a price, a product name, a
    /// basket total, a quantity — is a plain span, so the planner was asked to check that a total
    /// equals three times a price while being shown four buttons that all read "Add to basket". It
    /// did the right thing and refused to invent the targets, and every one of those scenarios was
    /// scored against it.
    /// </para>
    /// <para>
    /// A test id is a deliberate signal from whoever wrote the application that this element is
    /// worth addressing, which makes it exactly the thing a planner should be shown. The text is
    /// carried too, because "what does it currently say" is most of what an assertion needs.
    /// </para>
    /// </remarks>
    private static async Task AddTestIdElementsAsync(IPage page, List<SnapshotElement> elements, HashSet<string> seen)
    {
        var found = page.Locator("[data-testid]");
        var count = Math.Min(await found.CountAsync().ConfigureAwait(false), MaxElementsPerPage);

        for (var index = 0; index < count && elements.Count < MaxElementsPerPage; index++)
        {
            var element = found.Nth(index);

            if (await AttributeAsync(element, "data-testid").ConfigureAwait(false) is not { Length: > 0 } testId
                || !seen.Add(testId))
            {
                continue;
            }

            var text = await FirstTextAsync(element).ConfigureAwait(false);

            elements.Add(new SnapshotElement("text", text ?? testId, testId, text));
        }
    }

    /// <summary>
    /// The paths this page links to, within the origin. Query strings and fragments are dropped:
    /// two links to the same page are the same page, and a crawl that disagrees never finishes.
    /// </summary>
    private static async Task<IReadOnlyList<string>> LinksAsync(IPage page, Uri origin)
    {
        // Read one attribute at a time rather than with a page expression. Playwright would
        // happily evaluate a line of JavaScript here, and the worker is the one place in this
        // product where running a string as code must simply never appear (ADR-0019 §2); a test
        // holds the whole project to that, so this stays a loop.
        var links = page.Locator("a[href]");
        var count = Math.Min(await links.CountAsync().ConfigureAwait(false), MaxLinksPerPage);

        var paths = new List<string>();

        for (var index = 0; index < count; index++)
        {
            var href = await AttributeAsync(links.Nth(index), "href").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(href)
                || !Uri.TryCreate(origin, href, out var absolute)
                || !string.Equals(absolute.GetLeftPart(UriPartial.Authority), origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                // Off-origin links are not followed. The proxy would refuse them anyway; not
                // asking is cheaper than being refused.
                continue;
            }

            paths.Add(absolute.AbsolutePath);
        }

        return paths;
    }

    /// <summary>What a person would call this element.</summary>
    private static async Task<string?> TextOrAttributeAsync(ILocator element)
    {
        foreach (var attribute in new[] { "aria-label", "placeholder", "value", "name" })
        {
            if (await AttributeAsync(element, attribute).ConfigureAwait(false) is { Length: > 0 } label)
            {
                return label;
            }
        }

        return await FirstTextAsync(element).ConfigureAwait(false);
    }

    private static async Task<string?> FirstTextAsync(ILocator locator)
    {
        try
        {
            return await locator.CountAsync().ConfigureAwait(false) == 0
                ? null
                : Trim(await locator.First.InnerTextAsync().ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return null;
        }
    }

    private static async Task<string?> AttributeAsync(ILocator element, string attribute)
    {
        try
        {
            return Trim(await element.GetAttributeAsync(attribute).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// The HTML elements that carry a role without being told to, so that an ordinary page needs
    /// no ARIA to be described.
    /// </summary>
    private static string HtmlFor(string role) => role switch
    {
        "button" => "button, input[type=submit], input[type=button]",
        "link" => "a[href]",
        "textbox" => "input[type=text], input[type=email], input[type=password], input[type=search], input:not([type]), textarea",
        "checkbox" => "input[type=checkbox]",
        "radio" => "input[type=radio]",
        "combobox" => "select",
        "heading" => "h1, h2, h3",
        _ => role,
    };

    /// <summary>
    /// One line, no runs of whitespace, capped. A page can put a thousand words in a label, and
    /// all of it would go into a prompt.
    /// </summary>
    private static string? Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var single = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return single.Length > MaxTextLength ? single[..MaxTextLength] : single;
    }
}
