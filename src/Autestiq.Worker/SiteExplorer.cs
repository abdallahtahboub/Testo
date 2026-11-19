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

    /// <summary>How many containers out from a repeated element its distinguishing text is looked for in.</summary>
    private const int ContainerLevels = 3;

    /// <summary>How long the text that tells a repeated element apart may be.</summary>
    private const int MaxWithinLength = 60;

    /// <summary>How many things a container says are tried as a scope before an element is called unreachable.</summary>
    private const int MaxLabelsTried = 8;

    /// <summary>How many of a list's options are described. A country list has two hundred.</summary>
    private const int MaxOptionsDescribed = 25;

    /// <summary>
    /// How many pages reached by pressing are kept of one kind (<see cref="Signature"/>): enough for a form's
    /// different refusals, not a basket for every product (ADR-0024, 2026-11-18). Four is the smallest that leaves
    /// what our three applications show unchanged, with test ids and without; three dropped one page from two of them.
    /// </summary>
    private const int MaxPressedPagesPerKind = 4;

    /// <summary>Elements that hold text and nothing else: what a test checks when nothing has a test id.</summary>
    // Leaf divs and sections are in the list because sites lay out rows with them: eShopOnWeb's basket puts each line's
    // product name and price in bare sections inside an article, and without them the explorer saw a basket with
    // nothing in it (ADR-0024, 2026-11-18).
    private const string LeafTextSelector =
        "td:not(:has(*)), th:not(:has(*)), li:not(:has(*)), p:not(:has(*)), dd:not(:has(*)), dt:not(:has(*)), "
        + "span:not(:has(*)), strong:not(:has(*)), em:not(:has(*)), b:not(:has(*)), output:not(:has(*)), caption:not(:has(*)), "
        + "div:not(:has(*)), section:not(:has(*))";

    /// <summary>How many pages may be opened for each page the budget allows to be shown.</summary>
    private const int PagesOpenedPerPageShown = 4;

    /// <summary>The roles worth describing: the things a plan can act on or check.</summary>
    private static readonly string[] Roles =
        ["button", "link", "textbox", "searchbox", "checkbox", "radio", "combobox", "heading"];

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

        // The crawl reads the application as it opened, like every visit after it. An earlier exploration, or
        // run, may have left a booking or a paid invoice behind.
        if (startAfresh is not null && ExplorationPolicy.MayPressControls(job))
        {
            await startAfresh().ConfigureAwait(false);
        }

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

        // A cap on pages opened as well as pages shown. A kind whose samples differ is opened in full,
        // and an application can have thousands of one kind.
        var opened = 0;
        var maxOpened = job.Budget.MaxPagesCrawled * PagesOpenedPerPageShown;

        while (pages.Count < job.Budget.MaxPagesCrawled && opened < maxOpened && frontier.Next() is { } path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            opened++;

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

        // The test account's values never leave in a description: it goes to a model (ADR-0033).
        var snapshot = SecretRedaction.Without(new SiteSnapshot(job.TargetOrigin, pages, truncated), job.Secrets);

        return new Exploration(snapshot, visits, visitsFailed, refused);
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
    /// <remarks>
    /// Plain text without a test id counts by being there, not by what it says. It is the application's
    /// data — a weekday, a client's name, a description — and two invoices are one kind of page whatever they
    /// are for. Without this, describing text (ADR-0024) made every invoice and every date a kind of its own.
    /// </remarks>
    private static string Signature(PageSnapshot state) =>
        Masked(PathOnly(state.Path)) + "|"
        + string.Join(',', state.Elements.Select(element =>
            element.TestId is { Length: > 0 } testId ? Masked(testId)
            : element.Role == "text" ? "(text)"
            : Masked(element.Role + ":" + element.Name)));

    /// <summary>
    /// What makes two pages reached by pressing worth showing apart: their kind, and what their plain text
    /// says, with numbers masked. Two forms that refuse for different reasons look the same and say different
    /// things, and what they say is the point of pressing them.
    /// </summary>
    private static string Telling(PageSnapshot state) =>
        Masked(PathOnly(state.Path)) + "|"
        + string.Join(',', state.Elements.Select(element => Masked(element.TestId ?? element.Name)));

    /// <summary>
    /// What makes two pages the same page: where they are and everything they say. A catalogue whose
    /// basket link reads "Basket (1)" is the same kind of page as one that reads "Basket (0)", and not
    /// the same page.
    /// </summary>
    private static string ContentKey(PageSnapshot state) =>
        PathOnly(state.Path) + "\n" + state.Title + "\n" + state.Heading + "\n"
        + string.Join('\n', state.Elements.Select(element => $"{element.Role}\t{element.TestId}\t{element.Name}\t{element.Text}\t{element.Within}"));

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

        // In a tab of its own, closed afterwards. When there is no sitemap the browser shows its own error page
        // a moment after the 404, and in the crawl's tab that late navigation interrupted the first real page,
        // which was then left out: an application with no sitemap was described as having no pages at all.
        var probe = await page.Context.NewPageAsync().ConfigureAwait(false);

        try
        {
            // Through the browser, not through Playwright's own request context: that context
            // honours the ambient HTTP_PROXY of whatever machine the worker runs on, which is not
            // the run's egress proxy and answered 403 without the request ever reaching the
            // application. Every connection a run makes goes through the proxy ADR-0018 gives it,
            // and the browser is what that proxy is attached to.
            var response = await probe.GotoAsync(
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
        finally
        {
            await probe.CloseAsync().ConfigureAwait(false);
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
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            }
            catch (PlaywrightException exception) when (attempt < MaxOpenAttempts && WasInterrupted(exception))
            {
                // Another navigation took over before this one finished. The page asked for is fine, and is
                // opened again rather than left out of the description.
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
            {
                return null;
            }
        }
    }

    /// <summary>How many times a page is opened when another navigation keeps taking over.</summary>
    private const int MaxOpenAttempts = 3;

    private static bool WasInterrupted(PlaywrightException exception) =>
        exception.Message.Contains("interrupted by another navigation", StringComparison.Ordinal);

    private static async Task<PageSnapshot> DescribeAsync(IPage page, string path)
    {
        var title = await page.TitleAsync().ConfigureAwait(false);
        var heading = await FirstTextAsync(page.Locator("h1")).ConfigureAwait(false);

        var found = new List<(SnapshotElement Element, ILocator Locator)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in Roles)
        {
            var candidates = page.Locator($"[role='{role}'], {HtmlFor(role)}");
            var count = Math.Min(await candidates.CountAsync().ConfigureAwait(false), MaxElementsPerPage);

            for (var index = 0; index < count && found.Count < MaxElementsPerPage; index++)
            {
                var element = candidates.Nth(index);

                var (ariaRole, accessibleName) = await AccessibleOfAsync(element).ConfigureAwait(false);
                var name = Trim(accessibleName) ?? Trim(await TextOrAttributeAsync(element).ConfigureAwait(false));
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

                // The role the browser gives it, when that is one a target may name: a search field is a
                // "searchbox", and a target by role "textbox" would never find it.
                var described = ariaRole is { } given && Targets.NameableRoles.Contains(given) ? given : role;

                // A list says what it offers, so that a plan can choose by label.
                var options = described == "combobox" ? await OptionsOfAsync(element).ConfigureAwait(false) : null;

                found.Add((new SnapshotElement(described, name ?? testId ?? "", testId, options), element));
            }
        }

        await AddTestIdElementsAsync(page, found, seen).ConfigureAwait(false);
        await AddTextElementsAsync(page, found).ConfigureAwait(false);
        await ChooseHowToNameEachAsync(page, found).ConfigureAwait(false);

        return new PageSnapshot(path, Trim(title) ?? "", heading, [.. found.Select(item => item.Element)]);
    }

    /// <summary>
    /// Plain text that has no test id: the cells, items and short paragraphs a test most often checks.
    /// </summary>
    /// <remarks>
    /// Only leaf elements, ones with no element inside them, so that a row is not described as one long
    /// string and again as each of its cells. Added after everything with a role or a test id, so the
    /// limit on elements per page cuts plain text first (ADR-0024).
    /// </remarks>
    private static async Task AddTextElementsAsync(IPage page, List<(SnapshotElement Element, ILocator Locator)> found)
    {
        // Words a control or heading already says are that element's, not new text.
        var named = new HashSet<string>(found.Where(item => item.Element.Role != "text").Select(item => item.Element.Name), StringComparer.Ordinal);
        var candidates = page.Locator(LeafTextSelector);
        var count = Math.Min(await candidates.CountAsync().ConfigureAwait(false), MaxElementsPerPage * 2);

        for (var index = 0; index < count && found.Count < MaxElementsPerPage; index++)
        {
            var element = candidates.Nth(index);

            if (await AttributeAsync(element, "data-testid").ConfigureAwait(false) is not null)
            {
                // Described already, by its test id.
                continue;
            }

            if (Trim(await FirstTextAsync(element).ConfigureAwait(false)) is not { } text || named.Contains(text))
            {
                continue;
            }

            found.Add((new SnapshotElement("text", text, Text: text), element));
        }
    }

    /// <summary>
    /// Settles, for every element, the best way a plan can reach it, by asking the browser rather than guessing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The candidates are tried in order of what survives a change to the application best: a test id; then role
    /// and accessible name; then exact visible text; then each of those narrowed to the row, item or form the
    /// element sits in. The first one the browser finds exactly one element for is the one a plan is told to use.
    /// An element nothing reaches uniquely is marked so, and a plan does not lean on it (ADR-0024).
    /// </para>
    /// <para>
    /// Without test ids this is what a test's success turns on: a locator that matches two elements, or none,
    /// fails a step for a reason that has nothing to do with the application.
    /// </para>
    /// </remarks>
    private static async Task ChooseHowToNameEachAsync(IPage page, List<(SnapshotElement Element, ILocator Locator)> found)
    {
        for (var index = 0; index < found.Count; index++)
        {
            var (element, locator) = found[index];

            if (element.TestId is { Length: > 0 })
            {
                found[index] = found[index] with { Element = element with { NamedBy = TargetKind.TestId } };
                continue;
            }

            var (byRole, byText) = Candidates(element);
            var chosen = await FirstUniqueAsync(page, [byRole, byText]).ConfigureAwait(false);
            string? firstLabel = null;

            if (chosen is null && (byRole is not null || byText is not null))
            {
                // Not unique on its own: narrowed by each thing its row, item or form says, nearest first, until
                // the browser finds exactly one. The first cell is not always the one that tells it apart: two
                // services can cost the same, and only their names differ.
                var labels = await ContainerLabelsAsync(locator, element.Name).ConfigureAwait(false);
                firstLabel = labels.FirstOrDefault();

                foreach (var label in labels)
                {
                    if ((chosen = await FirstUniqueAsync(page, [byRole?.InsideOf(label), byText?.InsideOf(label)]).ConfigureAwait(false)) is not null)
                    {
                        break;
                    }
                }
            }

            found[index] = found[index] with
            {
                Element = element with { NamedBy = chosen?.Kind, Within = chosen is null ? firstLabel : chosen.Within },
            };
        }
    }

    /// <summary>The first of the targets the browser finds exactly one element for.</summary>
    private static async Task<ActionTarget?> FirstUniqueAsync(IPage page, ActionTarget?[] targets)
    {
        foreach (var target in targets)
        {
            if (target is not null && await FindsExactlyOneAsync(page, target).ConfigureAwait(false))
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>The two ways to reach an element without a test id: by role and name, and by its exact text.</summary>
    private static (ActionTarget? ByRole, ActionTarget? ByText) Candidates(SnapshotElement element)
    {
        var byRole = Targets.NameableRoles.Contains(element.Role) && element.Name.Length > 0
            ? ActionTarget.ByRole(element.Role, element.Name)
            : null;

        // A list's Text is its options, not text it shows.
        var text = element.Role == "combobox"
            ? null
            : element.Text ?? (element.Role is "text" or "button" or "link" or "heading" ? element.Name : null);

        return (byRole, text is { Length: > 0 } ? ActionTarget.ByText(text) : null);
    }

    /// <summary>Whether a target finds one element on the page, through the same code a planned run uses.</summary>
    private static async Task<bool> FindsExactlyOneAsync(IPage page, ActionTarget target)
    {
        try
        {
            return await Targets.Locate(page, target).CountAsync().ConfigureAwait(false) == 1;
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// What the browser calls an element: its role and its accessible name, which is what a target by role and
    /// name is matched against. A field labelled "Full name" is "Full name", whatever its <c>name</c> attribute
    /// says. Read through Playwright's own description of the page; nulls when it gives none.
    /// </summary>
    private static async Task<(string? Role, string? Name)> AccessibleOfAsync(ILocator element)
    {
        try
        {
            var described = await element.AriaSnapshotAsync(new LocatorAriaSnapshotOptions { Timeout = 5_000 }).ConfigureAwait(false);

            if (AccessiblePattern().Match(described.Split('\n', 2)[0]) is { Success: true } match)
            {
                var name = match.Groups[2].Success ? match.Groups[2].Value.Replace("\\\"", "\"", StringComparison.Ordinal) : null;
                return (match.Groups[1].Value, name is { Length: > 0 } ? name : null);
            }
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            // Described from its attributes and text instead.
        }

        return (null, null);
    }

    /// <summary>The role and name in the first line of Playwright's description of an element: <c>- textbox "Full name"</c>.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*-\s+([\w-]+)(?:\s+""((?:[^""\\]|\\.)*)"")?")]
    private static partial System.Text.RegularExpressions.Regex AccessiblePattern();

    /// <summary>
    /// What the containers around an element say, nearest first and without repeats: each single cell or label,
    /// then each line. In a list of products, the product's name and its price; in a table, every cell of the
    /// row. A button is often alone in a form of its own, inside the list item that says what it is for, so the
    /// containers around the nearest one are read too.
    /// </summary>
    private static async Task<List<string>> ContainerLabelsAsync(ILocator element, string name)
    {
        var labels = new List<string>();

        void Add(string? text)
        {
            if (Trim(text) is { Length: > 0 and <= MaxWithinLength } label
                && !string.Equals(label, name, StringComparison.Ordinal)
                && !labels.Contains(label)
                && labels.Count < MaxLabelsTried)
            {
                labels.Add(label);
            }
        }

        for (var level = 1; level <= ContainerLevels; level++)
        {
            // A fixed path of ours, to the nth nearest of the containers Targets.Containers lists.
            var container = element.Locator(
                "xpath=ancestor::*[self::tr or self::li or self::article or self::section or self::form or self::fieldset or self::dl or self::p]"
                + $"[{level.ToString(System.Globalization.CultureInfo.InvariantCulture)}]");

            try
            {
                if (await container.CountAsync().ConfigureAwait(false) != 1)
                {
                    break;
                }

                // Single cells and labels first: their text is the same to every way a browser reads a page. A
                // whole line, as the page is rendered, can join two cells in a way the browser's text filter does
                // not ("200g £8.50" on screen, "200g£8.50" underneath).
                var leaves = container.Locator(LeafTextSelector);
                var leafCount = Math.Min(await leaves.CountAsync().ConfigureAwait(false), MaxLabelsTried);

                for (var leaf = 0; leaf < leafCount; leaf++)
                {
                    Add(await FirstTextAsync(leaves.Nth(leaf)).ConfigureAwait(false));
                }

                // A table row's text comes as one line with its cells separated by tabs: each cell is a line here.
                // A line is taken whole or not at all: cutting the element's own text out of it would leave words
                // the page never shows (the "1" of a quantity taken out of "£11.25").
                var text = await container.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5_000 }).ConfigureAwait(false);

                foreach (var line in text.Split(['\n', '\t']))
                {
                    Add(line);
                }
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
            {
                // A container that will not give up its text cannot tell its element apart.
                break;
            }
        }

        return labels;
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
    private static async Task AddTestIdElementsAsync(IPage page, List<(SnapshotElement Element, ILocator Locator)> found, HashSet<string> seen)
    {
        var candidates = page.Locator("[data-testid]");
        var count = Math.Min(await candidates.CountAsync().ConfigureAwait(false), MaxElementsPerPage);

        for (var index = 0; index < count && found.Count < MaxElementsPerPage; index++)
        {
            var element = candidates.Nth(index);

            if (await AttributeAsync(element, "data-testid").ConfigureAwait(false) is not { Length: > 0 } testId
                || !seen.Add(testId))
            {
                continue;
            }

            var text = await FirstTextAsync(element).ConfigureAwait(false);

            found.Add((new SnapshotElement("text", text ?? testId, testId, text), element));
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

    /// <summary>A list's option labels, separated by " | ", as many as fit: the words a select action chooses by.</summary>
    private static async Task<string?> OptionsOfAsync(ILocator list)
    {
        try
        {
            var labels = (await list.Locator("option").AllTextContentsAsync().ConfigureAwait(false))
                .Select(Trim)
                .OfType<string>()
                .Take(MaxOptionsDescribed);

            return Trim(string.Join(" | ", labels));
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// The text an element holds, as the page wrote it rather than as it is drawn: a stylesheet that shows a
    /// name in capitals does not change what the browser's text filter and its text assertions compare with.
    /// </summary>
    private static async Task<string?> FirstTextAsync(ILocator locator)
    {
        try
        {
            return await locator.CountAsync().ConfigureAwait(false) == 0
                ? null
                : Trim(await locator.First.TextContentAsync().ConfigureAwait(false));
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
