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
    private sealed record Pressing(IReadOnlyList<PageSnapshot> Pages, bool Truncated, int Visits, int Failed, int Withheld = 0);

    /// <summary>
    /// Where one visit ended. No page means it could not be carried out, or, when <see cref="Withheld"/>, that it was
    /// carried out under the guard and is not to be described (ADR-0041).
    /// </summary>
    private readonly record struct Visit(PageSnapshot? Page, FormKind Form, bool AsksForPassword = false, bool Withheld = false)
    {
        public static Visit Failed => new(null, FormKind.None);

        public static Visit NotDescribed => new(null, FormKind.None, Withheld: true);
    }

    /// <summary>
    /// Whether the presses of a visit tried to send something: a blocked request after they began whose method and path
    /// had not been blocked while pages were only being opened.
    /// </summary>
    /// <remarks>
    /// An application that polls its server sends the same request again and again whatever is pressed: OWASP Juice
    /// Shop's live updates post to <c>/socket.io/</c> every few seconds, and counting every block withheld 11 of 19 visits
    /// (R9, 2026-11-25). That request is the page's, already blocked while it opened. The cost: a press whose only
    /// write is that same request is described, as the page without it.
    /// </remarks>
    private static bool PressSentSomething(ReadOnlyGuard guard, int blockedBefore, HashSet<string> background) =>
        guard.Blocked.Skip(blockedBefore).Any(request => !background.Contains(request));

    /// <summary>
    /// Whether pressing this element would send a form: a button inside one, or naming one, whose type is not
    /// "button" or "reset" (a button sends by default, and a type it does not know is the default), or a submit or
    /// image input. Asked by locators of ours, never by a script: the worker runs no string in the page.
    /// </summary>
    private static async Task<bool> SendsAFormAsync(ILocator control)
    {
        var inAForm = await control.Locator("xpath=ancestor::form[1]").CountAsync().ConfigureAwait(false) == 1
            || await AttributeAsync(control, "form").ConfigureAwait(false) is { Length: > 0 };

        if (!inAForm)
        {
            return false;
        }

        var type = (await AttributeAsync(control, "type").ConfigureAwait(false) ?? "").Trim().ToLowerInvariant();

        if (await control.Locator("xpath=self::button").CountAsync().ConfigureAwait(false) == 1)
        {
            return type is not ("button" or "reset");
        }

        return await control.Locator("xpath=self::input").CountAsync().ConfigureAwait(false) == 1 && type is ("submit" or "image");
    }

    /// <summary>One control worth pressing: how to reach it, and whether it is a button.</summary>
    private readonly record struct Control(ActionTarget Target, bool IsButton);

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
        ReadOnlyGuard? guard,
        DateTimeOffset stopBy,
        CancellationToken cancellationToken)
    {
        // Under the guard nothing is typed, and no form is sent (ADR-0041).
        var mayType = guard is null && ExplorationPolicy.MaySubmitForms(job);

        // Signing in is typing into a form, so it needs what typing needs, and the account itself. The dispatcher
        // hands one over only where the environment's owner allowed exploring to act (ADR-0032, ADR-0033).
        var maySignIn = mayType && FormValues.CanSignIn(job.Secrets);

        // What is already shown. By what pages say, not only their kind: two refusals with different messages
        // are both worth a plan's attention.
        var knownKinds = new HashSet<string>(crawled.Select(Telling), StringComparer.Ordinal);

        // How many pressed-to pages of each kind are kept: a basket holding each of a shop's products is one kind of
        // page that says something different every time, and without a limit it fills the budget and the prompt.
        var keptOfKind = new Dictionary<string, int>(StringComparer.Ordinal);
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
        var withheld = 0;

        // What pages send by themselves, blocked while they were only being opened: the crawl's, and each visit's own
        // opening. A press is judged by what it sent beyond that (PressSentSomething).
        var background = new HashSet<string>(guard?.Blocked ?? [], StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (start, presses, state) = queue.Dequeue();

            if (presses.Count >= MaxPressDepth)
            {
                continue;
            }

            var last = presses.Count > 0 ? KindOf(presses[^1].Target) : null;

            foreach (var control in Controls(state, untouched: presses.Count == 0, last, mayType))
            {
                // The plain press comes first, and says what the control's form holds. Filling the form
                // is only worth a visit when there is something to fill.
                var fills = new List<FillMode> { FillMode.None };

                for (var index = 0; index < fills.Count; index++)
                {
                    if (visits >= MaxVisits || found.Count >= MaxExploredStates || TimeProvider.System.GetUtcNow() >= stopBy)
                    {
                        // Said rather than hidden, for the same reason as the crawl's own budget.
                        return new Pressing(found, Truncated: true, visits, failed, withheld);
                    }

                    var reached = new List<PressStep>(presses) { new(control.Target, fills[index]) };
                    var askForm = mayType && control.IsButton && index == 0;

                    var visit = await VisitAsync(page, job, start, reached, mayType, askForm, described, startAfresh, guard, background, cancellationToken)
                        .ConfigureAwait(false);

                    if (visit.Withheld)
                    {
                        // Carried out, and not described: what it showed is not what the application does.
                        visits++;
                        withheld++;
                        break;
                    }

                    if (visit.Page is null)
                    {
                        // Once more, from the start. A visit that fails once and works the second time was
                        // the browser or the network; one that fails twice is the path.
                        visit = await VisitAsync(page, job, start, reached, mayType, askForm, described, startAfresh, guard, background, cancellationToken)
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

                        if (maySignIn && visit.AsksForPassword)
                        {
                            fills.Add(FillMode.SignIn);
                        }
                    }

                    var arrived = visit.Page with { ReachedBy = Steps(start, reached) };
                    var kind = Signature(arrived);
                    var full = keptOfKind.GetValueOrDefault(kind) >= MaxPressedPagesPerKind;

                    // The snapshot holds one page of each kind. The same page for another date, or with
                    // another reference number, would only fill the prompt. What a page says sets it apart
                    // (two refusals with different messages), but only so many times for one kind.
                    if (!full && knownKinds.Add(Telling(arrived)))
                    {
                        found.Add(arrived);
                        keptOfKind[kind] = keptOfKind.GetValueOrDefault(kind) + 1;
                    }

                    // Nor is a kind already shown that often explored further from.
                    if (!full && searched.Add(ContentKey(arrived)))
                    {
                        queue.Enqueue((start, reached, arrived));
                    }
                }
            }
        }

        return new Pressing(found, Truncated: false, visits, failed, withheld);
    }

    /// <summary>The controls of one page worth pressing, in the order the page offers them.</summary>
    /// <param name="state">The page.</param>
    /// <param name="untouched">Whether nothing has been pressed on the way here.</param>
    /// <param name="last">The control pressed to get here, when one was.</param>
    /// <param name="mayType">Whether forms may be filled and sent.</param>
    private static IEnumerable<Control> Controls(PageSnapshot state, bool untouched, string? lastKind, bool mayType)
    {
        var kinds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in state.Elements)
        {
            if (kinds.Count >= MaxControlsPerState)
            {
                yield break;
            }

            // The best way to reach it: its test id, or else its role and name, told apart from others like it
            // by the container it sits in. A repeat nothing tells apart cannot be pressed (ADR-0024).
            if (TargetFor(element) is not { } target)
            {
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

            var kind = KindOf(target);

            // The same control twice running rarely shows what the first press did not.
            if (string.Equals(kind, lastKind, StringComparison.Ordinal))
            {
                continue;
            }

            var words = element.TestId + " " + element.Name;

            if (NeverPressedPattern().IsMatch(words) || (!mayType && SendsAFormPattern().IsMatch(words)))
            {
                continue;
            }

            // date-16 and date-17 are one kind of control, and one of a kind is pressed.
            if (kinds.Add(kind))
            {
                yield return new Control(target, isButton);
            }
        }
    }

    /// <summary>
    /// How a control is reached: the way the describing step confirmed finds it and nothing else
    /// (<c>ChooseHowToNameEachAsync</c>). Null when no way does, and then it is not pressed.
    /// </summary>
    private static ActionTarget? TargetFor(SnapshotElement element)
    {
        if (element.Role is not ("button" or "link"))
        {
            return null;
        }

        ActionTarget? target = element.NamedBy switch
        {
            TargetKind.TestId when element.TestId is { Length: > 0 } testId => ActionTarget.ByTestId(testId),
            TargetKind.RoleAndName when element.Name.Length > 0 => ActionTarget.ByRole(element.Role, element.Name),
            TargetKind.Text when (element.Text ?? element.Name) is { Length: > 0 } text => ActionTarget.ByText(text),
            _ => null,
        };

        return target is not null && element.NamedBy != TargetKind.TestId && element.Within is { Length: > 0 } within
            ? target.InsideOf(within)
            : target;
    }

    /// <summary>What makes two controls the same kind: date-16 and date-17 are one kind of control.</summary>
    private static string KindOf(ActionTarget target) =>
        Masked($"{target.Kind}:{target.Role}:{target.Value}:{target.Within}");

    /// <summary>How a path is put to a planner: where it started and what was done, in a plan's own words.</summary>
    private static List<string> Steps(string start, List<PressStep> presses) =>
    [
        "open " + start,
        .. presses.Select(press => press.Fill switch
        {
            FillMode.Valid => "type test values, then click " + Named(press.Target),
            FillMode.WrongEmail => "type test values with an email address that is not one, then click " + Named(press.Target),

            // Named, never the values: this text goes to the planner.
            FillMode.SignIn => "sign in with the test account (secrets username and password), then click " + Named(press.Target),
            _ => "click " + Named(press.Target),
        }),
    ];

    /// <summary>How a press's control is put in a plan's own words: its test id, or what it is and what it says.</summary>
    private static string Named(ActionTarget target) => target.Kind == TargetKind.TestId
        ? target.Value
        : $"the {target.Role} \"{target.Value}\"" + (target.Within is { Length: > 0 } within ? $" within \"{within}\"" : "");

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
        ReadOnlyGuard? guard,
        HashSet<string> background,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (startAfresh is not null)
        {
            await startAfresh().ConfigureAwait(false);
        }

        await page.Context.ClearCookiesAsync().ConfigureAwait(false);

        var openedFrom = guard?.Blocked.Count ?? 0;

        if (await Open(page, start).ConfigureAwait(false) is null)
        {
            return Visit.Failed;
        }

        if (guard is not null)
        {
            background.UnionWith(guard.Blocked.Skip(openedFrom));
        }

        var form = FormKind.None;
        var asksForPassword = false;

        // What the page itself tried to send while it opened is the page's, not the presses'.
        var blockedBefore = guard?.Blocked.Count ?? 0;

        try
        {
            for (var index = 0; index < presses.Count; index++)
            {
                var press = presses[index];

                // Through the one place that turns a target into an element: nothing from the application is
                // ever put into a selector as text.
                var target = Targets.Locate(page, press.Target);

                if (await target.CountAsync().ConfigureAwait(false) != 1)
                {
                    return Visit.Failed;
                }

                // Under the guard a form is never sent, whatever its method or its button is called (ADR-0041).
                if (guard is not null && await SendsAFormAsync(target).ConfigureAwait(false))
                {
                    return Visit.NotDescribed;
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
                        asksForPassword = fields.Any(field => field.Type == "password");
                    }

                    if (typing)
                    {
                        foreach (var (field, type, isEmail) in fields)
                        {
                            var value = press.Fill == FillMode.SignIn && job.Secrets is { } secrets
                                ? FormValues.ForSignIn(type, isEmail, secrets)
                                : FormValues.For(type, isEmail, press.Fill);

                            await field.FillAsync(
                                value,
                                new LocatorFillOptions { Timeout = ActionTimeout }).ConfigureAwait(false);
                        }
                    }
                }

                await PressAndSettleAsync(page, target).ConfigureAwait(false);
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

        // A press whose page tried to send something showed the page without it: an error, a spinner, or nothing. That
        // is not what the control does, so it is not told to a planner as though it were.
        if (guard is not null && PressSentSomething(guard, blockedBefore, background))
        {
            return Visit.NotDescribed;
        }

        return new Visit(await LookAtAsync(page, AddressOf(here), described).ConfigureAwait(false), form, asksForPassword);
    }

    /// <summary>
    /// Presses a control and waits for what it leads to.
    /// </summary>
    /// <remarks>
    /// The navigation is listened for before the click, not after it. Waiting for the page to be loaded once the
    /// click has returned found the page from before the click still loaded, because the browser had not begun
    /// the new navigation yet: on a quiet machine it began within a millisecond and the new page was read, and on
    /// a busy one the old page was read or the read failed halfway. The same application was then described
    /// differently from one run to the next. A control that leads nowhere costs <see cref="ActionTimeout"/>.
    /// </remarks>
    private static async Task PressAndSettleAsync(IPage page, ILocator target)
    {
        var navigated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigated(object? sender, IFrame frame)
        {
            if (frame == page.MainFrame)
            {
                navigated.TrySetResult();
            }
        }

        page.FrameNavigated += OnNavigated;

        try
        {
            // A click that fails fails the visit, as before: only the wait for a navigation may time out quietly.
            await target.ClickAsync(new LocatorClickOptions { Timeout = ActionTimeout }).ConfigureAwait(false);

            try
            {
                await navigated.Task.WaitAsync(TimeSpan.FromMilliseconds(ActionTimeout), TimeProvider.System).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Nothing navigated within the limit: the control changed the page in place, or did nothing, and the
                // page as it now is is the one to read.
            }
        }
        finally
        {
            page.FrameNavigated -= OnNavigated;
        }

        await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);

        // What a press changed in place may still be drawing.
        await SettleAsync(page).ConfigureAwait(false);
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
