using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Worker;

/// <summary>What an exploration found, and what it took.</summary>
/// <param name="Snapshot">The description of the application. The only part that goes any further.</param>
/// <param name="Visits">How many fresh visits were made to press controls. Each is a page load and a few presses.</param>
/// <param name="VisitsFailed">How many of those could not be carried out, after one retry.</param>
/// <param name="PressingRefused">Whether pressing controls was asked for and refused by <see cref="ExplorationPolicy"/>.</param>
internal sealed record Exploration(SiteSnapshot Snapshot, int Visits, int VisitsFailed, bool PressingRefused);

/// <summary>
/// Walks the target and describes what is on it, so that a plan can be made against an application
/// as it actually is rather than as somebody imagined it.
/// </summary>
/// <remarks>
/// <para>
/// No model is involved here, and that is the point (ADR-0019 §5). The crawl is a breadth-first
/// walk of in-origin links from the start page, within a budget, so the same application produces
/// the same description — which is what makes a plan reproducible and a flake rate meaningful.
/// A model choosing where to look could be led somewhere by the page it is reading.
/// </para>
/// <para>
/// When the job asks for it and <see cref="ExplorationPolicy"/> allows it (ADR-0021, ADR-0022), the
/// explorer also presses the application's own controls and says what each one leads to, by the
/// same fixed, bounded rule. That part is in <c>SiteExplorer.Pressing.cs</c>.
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

    /// <summary>The roles worth describing: the things a plan can act on or check.</summary>
    private static readonly string[] Roles =
        ["button", "link", "textbox", "checkbox", "radio", "combobox", "heading"];

    /// <summary>
    /// Describes the target: the pages its links and its sitemap reach, within the page budget, and,
    /// where the job asks and the policy allows, what its controls do.
    /// </summary>
    /// <param name="page">The browser page to drive.</param>
    /// <param name="job">The job: the target, the budget and what was asked for.</param>
    /// <param name="startAfresh">
    /// Puts the target back to its starting state, for an application that keeps state on its server.
    /// Called before every visit that presses something. Null for an application whose state lives in
    /// the browser, which clearing the cookies resets.
    /// </param>
    /// <param name="cancellationToken">Stops the exploration.</param>
    public static async Task<Exploration> ExploreAsync(
        IPage page,
        WorkerJob job,
        Func<Task>? startAfresh,
        CancellationToken cancellationToken)
    {
        var origin = new Uri(job.TargetOrigin);

        // Which address is opened next, and whether the crawl has stopped short when it stops, is the
        // frontier's to decide: one address of every kind first (CrawlFrontier).
        var frontier = new CrawlFrontier("/");

        // Pages the application itself advertises. Following links alone is not enough: the demo
        // shop hides its checkout behind a basket that has something in it, so a crawl that starts
        // with an empty basket never reaches /checkout, /order/placed or /account — and six of the
        // first benchmark's twelve scenarios were about pages the planner had never been shown.
        // A real application publishes a sitemap for exactly this reason.
        foreach (var advertised in await SitemapPathsAsync(page, origin).ConfigureAwait(false))
        {
            frontier.Note(advertised);
        }

        var pages = new List<PageSnapshot>();
        var described = new Dictionary<string, PageSnapshot>(StringComparer.Ordinal);
        var kindsShown = new HashSet<string>(StringComparer.Ordinal);

        while (pages.Count < job.Budget.MaxPagesCrawled && frontier.Next() is { } path)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await Open(page, path).ConfigureAwait(false);

            if (response is null)
            {
                continue;
            }

            var looked = await LookAtAsync(page, path, described).ConfigureAwait(false);
            var shape = Signature(looked);

            frontier.Saw(path, shape);

            // One page of each kind is shown. The same page for another date says nothing a plan
            // needs, and every page shown is paid for in every prompt.
            if (kindsShown.Add(shape))
            {
                pages.Add(looked);
            }

            foreach (var link in await LinksAsync(page, origin).ConfigureAwait(false))
            {
                frontier.Note(link);
            }
        }

        // Said rather than hidden: a planner that does not know it is looking at part of an
        // application will plan as though it were all of it. And not said when it is not so: a planner
        // told it has seen only part will hedge every plan it writes.
        var truncated = frontier.HasUnseen;

        var visits = 0;
        var visitsFailed = 0;
        var refused = false;

        if (job.ExploreStates)
        {
            if (ExplorationPolicy.MayPressControls(job))
            {
                var pressed = await PressAsync(page, job, pages, described, startAfresh, cancellationToken).ConfigureAwait(false);
                pages.AddRange(pressed.Pages);
                truncated |= pressed.Truncated;
                visits = pressed.Visits;
                visitsFailed = pressed.Failed;
            }
            else
            {
                // Asked for and not done. Said, so that nobody reads a description made without pressing
                // anything as one made with it.
                refused = true;
            }
        }

        return new Exploration(new SiteSnapshot(job.TargetOrigin, pages, truncated), visits, visitsFailed, refused);
    }

    /// <summary>
    /// Describes the page the browser is on, or recalls the description when this exact page has been
    /// described before.
    /// </summary>
    /// <remarks>
    /// Describing a page is a hundred questions to the browser, and pressing controls lands on the same
    /// few pages again and again. So the page's markup is hashed, and a page whose hash has been seen is
    /// not described twice. Only the hash is kept: the markup never leaves this method, and nothing of it
    /// reaches a snapshot.
    /// </remarks>
    private static async Task<PageSnapshot> LookAtAsync(IPage page, string path, Dictionary<string, PageSnapshot> described)
    {
        string key;

        try
        {
            key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(await page.ContentAsync().ConfigureAwait(false))));
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            // A page that will not give up its markup is described the long way, and not remembered.
            return await DescribeAsync(page, path).ConfigureAwait(false);
        }

        if (described.TryGetValue(key, out var known))
        {
            return known with { Path = path, ReachedBy = null };
        }

        var fresh = await DescribeAsync(page, path).ConfigureAwait(false);
        described[key] = fresh;

        return fresh;
    }

    /// <summary>
    /// What makes two pages the same kind of page: where they are and what is on them, with numbers
    /// masked. The same page for another date, or with another booking reference, is one kind.
    /// </summary>
    private static string Signature(PageSnapshot state) =>
        Masked(PathOnly(state.Path)) + "|"
        + string.Join(',', state.Elements.Select(element => Masked(element.TestId ?? element.Name)));

    /// <summary>
    /// What makes two pages the same page: where they are and everything they say. A catalogue whose
    /// basket link reads "Basket (1)" is the same kind of page as one that reads "Basket (0)", and not
    /// the same page.
    /// </summary>
    private static string ContentKey(PageSnapshot state) =>
        PathOnly(state.Path) + "\n" + state.Title + "\n" + state.Heading + "\n"
        + string.Join('\n', state.Elements.Select(element => $"{element.Role}\t{element.TestId}\t{element.Name}\t{element.Text}"));

    /// <summary>A path without its query string.</summary>
    private static string PathOnly(string path)
    {
        var query = path.IndexOf('?', StringComparison.Ordinal);

        return query < 0 ? path : path[..query];
    }

    /// <summary>Numbers, as in a date or a time, are the same kind whatever their value.</summary>
    private static string Masked(string text) => DigitsPattern().Replace(text, "#");

    [System.Text.RegularExpressions.GeneratedRegex(@"\d+")]
    private static partial System.Text.RegularExpressions.Regex DigitsPattern();

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
                paths.Add(absolute.PathAndQuery);
            }
        }

        return paths;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<loc>\s*([^<]+?)\s*</loc>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex LocationPattern();

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

            paths.Add(absolute.PathAndQuery);
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
