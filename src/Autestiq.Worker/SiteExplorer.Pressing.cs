using Microsoft.Playwright;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;

namespace Autestiq.Worker;

/// <content>
/// Pressing the application's controls to find out what they lead to (ADR-0021), and typing fixed
/// values into its forms first where that is allowed (ADR-0022).
/// </content>
internal static partial class SiteExplorer
{
    /// <summary>How many visits are made to press controls, in all. Each is a fresh page load, so this is what pressing costs.</summary>
    private const int MaxVisits = 150;

    /// <summary>
    /// How many presses one path may take after the page it starts on. Three reaches a basket with two
    /// products: add one, go back to the catalogue, add another.
    /// </summary>
    private const int MaxPressDepth = 3;

    /// <summary>How many controls of one page are pressed. The rest are described, not exercised.</summary>
    private const int MaxControlsPerState = 12;

    /// <summary>How many fields of one form are typed into.</summary>
    private const int MaxFieldsPerForm = 20;

    /// <summary>How many pages pressing may add to a snapshot.</summary>
    private const int MaxExploredStates = 40;

    /// <summary>
    /// How long one press or one field may take, in milliseconds. Shorter than a run's own limit: a
    /// control that cannot be pressed should cost seconds, not a quarter of a minute, a hundred times over.
    /// </summary>
    private const float ActionTimeout = 5_000;

    /// <summary>What can be typed into. Not what is chosen, ticked, uploaded or hidden.</summary>
    private const string FieldSelector =
        "input:not([type=hidden]):not([type=submit]):not([type=button]):not([type=reset]):not([type=image])"
        + ":not([type=checkbox]):not([type=radio]):not([type=file]):not([type=color]):not([type=range])"
        + ":not([type=month]):not([type=week]):not([type=datetime-local]), textarea";

    /// <summary>Where a field says what it is for, when its type does not.</summary>
    private static readonly string[] NamingAttributes = ["name", "id", "data-testid", "autocomplete"];

    /// <summary>What pressing found, and what it took.</summary>
    private sealed record Pressing(IReadOnlyList<PageSnapshot> Pages, bool Truncated, int Visits, int Failed);

    /// <summary>Where one visit ended. No page means it could not be carried out.</summary>
    private readonly record struct Visit(PageSnapshot? Page, FormKind Form)
    {
        public static Visit Failed => new(null, FormKind.None);
    }

    /// <summary>One control worth pressing.</summary>
    private readonly record struct Control(string TestId, bool IsButton);

    /// <summary>
    /// Presses the application's controls, one path at a time, and describes what each path reaches.
    /// This is what lets a plan know that "add to basket" leads to the basket, and what a form says
    /// when it is sent, before the plan is written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Every visit starts afresh.</strong> The target is put back to its starting state where
    /// it keeps any, the cookies are cleared, and the page the path starts on is opened again. No path
    /// inherits what an earlier one did, so a page's "reached by" is true on a fresh application, which
    /// is where a plan will run.
    /// </para>
    /// <para>
    /// <strong>Every crawled page is a place to start.</strong> A form five links deep is one press
    /// away from its own page, so the search stays shallow. On a page nothing has been pressed on, links
    /// are left alone: the crawl has followed them already. After a press they are followed, because the
    /// press may have changed what they lead to.
    /// </para>
    /// <para>
    /// <strong>A page is searched further only when it has not been seen before</strong>, word for
    /// word. Pressing "search" with nothing typed lands on the catalogue again, and there is nothing
    /// new to do from it. A catalogue whose basket link now reads "Basket (1)" is not the catalogue
    /// seen before, and is searched. What a page does not show cannot be told apart: a catalogue reads
    /// the same with a teapot in the basket as with a cup, so only the first of those is searched.
    /// </para>
    /// <para>
    /// The order is breadth first and fixed by the application, and the limits are counts rather than
    /// times, so the same application is explored the same way every run.
    /// </para>
    /// </remarks>
    private static async Task<Pressing> PressAsync(
        IPage page,
        WorkerJob job,
        IReadOnlyList<PageSnapshot> crawled,
        Dictionary<string, PageSnapshot> described,
        Func<Task>? startAfresh,
        CancellationToken cancellationToken)
    {
        var mayType = ExplorationPolicy.MaySubmitForms(job);

        var knownKinds = new HashSet<string>(crawled.Select(Signature), StringComparer.Ordinal);
        var searched = new HashSet<string>(StringComparer.Ordinal);
        var found = new List<PageSnapshot>();
        var queue = new Queue<(string Start, List<PressStep> Presses, PageSnapshot State)>();

        foreach (var start in crawled)
        {
            // Two addresses that show the same thing are one place to start.
            if (searched.Add(ContentKey(start)))
            {
                queue.Enqueue((start.Path, [], start));
            }
        }

        var visits = 0;
        var failed = 0;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (start, presses, state) = queue.Dequeue();

            if (presses.Count >= MaxPressDepth)
            {
                continue;
            }

            var last = presses.Count > 0 ? presses[^1].TestId : null;

            foreach (var control in Controls(state, untouched: presses.Count == 0, last, mayType))
            {
                // The plain press comes first, and says what the control's form holds. Filling the form
                // is only worth a visit when there is something to fill.
                var fills = new List<FillMode> { FillMode.None };

                for (var index = 0; index < fills.Count; index++)
                {
                    if (visits >= MaxVisits || found.Count >= MaxExploredStates)
                    {
                        // Said rather than hidden, for the same reason as the crawl's own budget.
                        return new Pressing(found, Truncated: true, visits, failed);
                    }

                    var reached = new List<PressStep>(presses) { new(control.TestId, fills[index]) };
                    var askForm = mayType && control.IsButton && index == 0;

                    var visit = await VisitAsync(page, job, start, reached, mayType, askForm, described, startAfresh, cancellationToken)
                        .ConfigureAwait(false);

                    if (visit.Page is null)
                    {
                        // Once more, from the start. A visit that fails once and works the second time was
                        // the browser or the network; one that fails twice is the path.
                        visit = await VisitAsync(page, job, start, reached, mayType, askForm, described, startAfresh, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    visits++;

                    if (visit.Page is null)
                    {
                        failed++;
                        break;
                    }

                    if (askForm && visit.Form != FormKind.None)
                    {
                        fills.Add(FillMode.Valid);

                        if (visit.Form == FormKind.FieldsWithEmail)
                        {
                            fills.Add(FillMode.WrongEmail);
                        }
                    }

                    var arrived = visit.Page with { ReachedBy = Steps(start, reached) };

                    // The snapshot holds one page of each kind. The same page for another date, or with
                    // another reference number, would only fill the prompt.
                    if (knownKinds.Add(Signature(arrived)))
                    {
                        found.Add(arrived);
                    }

                    if (searched.Add(ContentKey(arrived)))
                    {
                        queue.Enqueue((start, reached, arrived));
                    }
                }
            }
        }

        return new Pressing(found, Truncated: false, visits, failed);
    }

    /// <summary>The controls of one page worth pressing, in the order the page offers them.</summary>
    /// <param name="state">The page.</param>
    /// <param name="untouched">Whether nothing has been pressed on the way here.</param>
    /// <param name="last">The control pressed to get here, when one was.</param>
    /// <param name="mayType">Whether forms may be filled and sent.</param>
    private static IEnumerable<Control> Controls(PageSnapshot state, bool untouched, string? last, bool mayType)
    {
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        var lastKind = last is null ? null : Masked(last);

        foreach (var element in state.Elements)
        {
            if (kinds.Count >= MaxControlsPerState)
            {
                yield break;
            }

            if (element.TestId is not { Length: > 0 } testId)
            {
                // Only what the application's authors marked as worth addressing is pressed.
                continue;
            }

            var isButton = element.Role == "button";

            if (!isButton && element.Role != "link")
            {
                continue;
            }

            // A link on a page nothing has been pressed on leads where the crawl has already been.
            if (!isButton && untouched)
            {
                continue;
            }

            var kind = Masked(testId);

            // The same control twice running rarely shows what the first press did not.
            if (string.Equals(kind, lastKind, StringComparison.Ordinal))
            {
                continue;
            }

            var words = testId + " " + element.Name;

            if (NeverPressedPattern().IsMatch(words) || (!mayType && SendsAFormPattern().IsMatch(words)))
            {
                continue;
            }

            // date-16 and date-17 are one kind of control, and one of a kind is pressed.
            if (kinds.Add(kind))
            {
                yield return new Control(testId, isButton);
            }
        }
    }

    /// <summary>How a path is put to a planner: where it started and what was done, in a plan's own words.</summary>
    private static List<string> Steps(string start, List<PressStep> presses) =>
    [
        "open " + start,
        .. presses.Select(press => press.Fill switch
        {
            FillMode.Valid => "type test values, then click " + press.TestId,
            FillMode.WrongEmail => "type test values with an email address that is not one, then click " + press.TestId,
            _ => "click " + press.TestId,
        }),
    ];

    /// <summary>
    /// Makes one visit: starts afresh, opens the page the path starts on, carries out each press, and
    /// describes where that ends.
    /// </summary>
    /// <param name="askForm">Whether to say what the last control's form holds, which costs a few questions.</param>
    private static async Task<Visit> VisitAsync(
        IPage page,
        WorkerJob job,
        string start,
        List<PressStep> presses,
        bool mayType,
        bool askForm,
        Dictionary<string, PageSnapshot> described,
        Func<Task>? startAfresh,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (startAfresh is not null)
        {
            await startAfresh().ConfigureAwait(false);
        }

        await page.Context.ClearCookiesAsync().ConfigureAwait(false);

        if (await Open(page, start).ConfigureAwait(false) is null)
        {
            return Visit.Failed;
        }

        var form = FormKind.None;

        try
        {
            for (var index = 0; index < presses.Count; index++)
            {
                var press = presses[index];

                // By test id, through Playwright's own escaping: the id came from the application, and is
                // never put into a selector as text.
                var target = page.GetByTestId(press.TestId);

                if (await target.CountAsync().ConfigureAwait(false) != 1)
                {
                    return Visit.Failed;
                }

                var typing = mayType && press.Fill != FillMode.None;
                var asking = askForm && index == presses.Count - 1;

                if (typing || asking)
                {
                    var fields = await FieldsOfAsync(page, target).ConfigureAwait(false);

                    if (asking)
                    {
                        form = fields.Count == 0 ? FormKind.None
                            : fields.Any(field => field.IsEmail) ? FormKind.FieldsWithEmail
                            : FormKind.Fields;
                    }

                    if (typing)
                    {
                        foreach (var (field, type, isEmail) in fields)
                        {
                            await field.FillAsync(
                                FormValues.For(type, isEmail, press.Fill),
                                new LocatorFillOptions { Timeout = ActionTimeout }).ConfigureAwait(false);
                        }
                    }
                }

                await target.ClickAsync(new LocatorClickOptions { Timeout = ActionTimeout }).ConfigureAwait(false);
                await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return Visit.Failed;
        }

        if (!Uri.TryCreate(page.Url, UriKind.Absolute, out var here)
            || !string.Equals(
                here.GetLeftPart(UriPartial.Authority),
                new Uri(job.TargetOrigin).GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
        {
            // A press that leaves the target has nothing to say about the target.
            return Visit.Failed;
        }

        return new Visit(await LookAtAsync(page, here.PathAndQuery, described).ConfigureAwait(false), form);
    }

    /// <summary>
    /// The fields a control's press would send: those of the form it sits in, or of the page when it
    /// sits in none. Only fields a person could see and type into.
    /// </summary>
    private static async Task<List<(ILocator Field, string Type, bool IsEmail)>> FieldsOfAsync(IPage page, ILocator control)
    {
        // A fixed selector of ours. Nothing from the application is put into it.
        var form = control.Locator("xpath=ancestor::form[1]");

        var candidates = await form.CountAsync().ConfigureAwait(false) == 1
            ? form.Locator(FieldSelector)
            : page.Locator(FieldSelector);

        var count = Math.Min(await candidates.CountAsync().ConfigureAwait(false), MaxFieldsPerForm);
        var fields = new List<(ILocator Field, string Type, bool IsEmail)>();

        for (var index = 0; index < count; index++)
        {
            var field = candidates.Nth(index);

            if (!await field.IsVisibleAsync().ConfigureAwait(false) || !await field.IsEditableAsync().ConfigureAwait(false))
            {
                continue;
            }

            var type = (await AttributeAsync(field, "type").ConfigureAwait(false) ?? "text").ToLowerInvariant();

            fields.Add((field, type, type == "email" || await NamesAnEmailAsync(field).ConfigureAwait(false)));
        }

        return fields;
    }

    /// <summary>
    /// Whether a field says, by its name, that it is for an email address. Plenty of forms ask for one
    /// in a plain text field and check it themselves, and "Test value" would never get past them.
    /// </summary>
    private static async Task<bool> NamesAnEmailAsync(ILocator field)
    {
        foreach (var attribute in NamingAttributes)
        {
            if (await AttributeAsync(field, attribute).ConfigureAwait(false) is { } value
                && (value.Contains("email", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("e-mail", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Controls that are never pressed, on any application: the ones whose names say they spend money,
    /// destroy something, or end the session. Exploring exists to find states, not to buy things.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"order|pay|purchase|place|delete|cancel|sign-?out|log-?out",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex NeverPressedPattern();

    /// <summary>Controls named for sending a form. Pressed only where the policy allows forms to be sent.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"submit|confirm|send",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex SendsAFormPattern();
}
