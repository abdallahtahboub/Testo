using Microsoft.Playwright;
using Testo.Contracts.Execution;
using Testo.Contracts.Planning;

namespace Testo.Worker;

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
/// Everything it returns is untrusted input. It is kept to roles, accessible names, test ids and
/// trimmed text: no markup, no attributes, no URLs, nothing that reads as code.
/// </para>
/// </remarks>
internal static class SiteExplorer
{
    /// <summary>How many elements of one page are described. A page with more says so by being cut.</summary>
    private const int MaxElementsPerPage = 60;

    /// <summary>How long a name or a piece of text may be before it is trimmed.</summary>
    private const int MaxTextLength = 120;

    /// <summary>How many links of one page are followed. A navigation of a thousand items is a page, not a site.</summary>
    private const int MaxLinksPerPage = 40;

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

        return new SiteSnapshot(job.TargetOrigin, pages, truncated);
    }

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

                elements.Add(new SnapshotElement(role, name ?? testId ?? "", testId));
            }
        }

        return new PageSnapshot(path, Trim(title) ?? "", heading, elements);
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
