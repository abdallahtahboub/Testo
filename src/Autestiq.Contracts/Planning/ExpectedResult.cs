namespace Autestiq.Contracts.Planning;

/// <summary>
/// Something the tester says the application should show at the end of a test (ADR-0034): a documented expectation,
/// never something the explorer observed.
/// </summary>
/// <param name="Text">The text, exactly as the application shows it, such as <c>Thanks for your order</c>.</param>
/// <param name="Path">Where it appears, such as <c>/Basket/Success</c>, when the tester says so.</param>
public sealed record ExpectedResult(string Text, string? Path = null);

/// <summary>Where a check's expected value came from, decided by software and never by the model (ADR-0034 §3).</summary>
public enum CheckEvidence
{
    /// <summary>Its target or text is on a page the explorer saw.</summary>
    Observed = 1,

    /// <summary>It matches an expected result the tester stated.</summary>
    StatedByTester = 2,

    /// <summary>Neither: the plan assumed it, and a person should verify it.</summary>
    Assumed = 3,
}

/// <summary>The rules for expected results, in one place for the service, the validator and the labels (ADR-0034).</summary>
public static class ExpectedResults
{
    /// <summary>The most a test may carry.</summary>
    public const int MaxCount = 5;

    /// <summary>The longest text, and the longest path.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// The results as they are kept: text and path trimmed, an empty path dropped. <c>null</c> when any is not allowed:
    /// more than <see cref="MaxCount"/>, an empty or overlong text, a line break or other control character, or a path
    /// that does not start with <c>/</c>.
    /// </summary>
    public static IReadOnlyList<ExpectedResult>? Normalize(IReadOnlyList<ExpectedResult>? results)
    {
        if (results is null)
        {
            return [];
        }

        if (results.Count > MaxCount)
        {
            return null;
        }

        var kept = new List<ExpectedResult>(results.Count);

        foreach (var result in results)
        {
            var text = result?.Text?.Trim() ?? "";
            var path = result?.Path?.Trim() is { Length: > 0 } given ? given : null;

            if (text.Length is 0 or > MaxLength || HasControl(text))
            {
                return null;
            }

            if (path is not null && (path.Length > MaxLength || path[0] != '/' || HasControl(path) || path.Contains(' ', StringComparison.Ordinal)))
            {
                return null;
            }

            kept.Add(new ExpectedResult(text, path));
        }

        return kept;
    }

    /// <summary>
    /// Whether a check matches a stated result: an exact text, ignoring case and surrounding spaces, or for a URL check,
    /// its path. Only checks that something is there: a stated result says what is shown, not what is absent.
    /// </summary>
    public static bool States(PlannedAction action, IReadOnlyList<ExpectedResult>? results)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (results is not { Count: > 0 })
        {
            return false;
        }

        return action.Kind switch
        {
            ActionKind.AssertUrl => results.Any(result => Same(result.Path, action.Path)),
            ActionKind.AssertText => results.Any(result => Same(result.Text, action.Text)),
            ActionKind.AssertVisible or ActionKind.WaitFor => results.Any(result => Same(result.Text, action.Target?.Value)),
            _ => false,
        };
    }

    /// <summary>
    /// The stated results no check of the plan looks for (ADR-0035): a result counts as checked when a check that
    /// something is there (text or visible) matches its text. Its page, when given, says where it appears; checking the
    /// text is what makes the result tested.
    /// </summary>
    public static IReadOnlyList<ExpectedResult> Unchecked(IReadOnlyList<PlannedAction> actions, IReadOnlyList<ExpectedResult>? results)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (results is not { Count: > 0 })
        {
            return [];
        }

        var checks = actions.Where(action => action.Kind is ActionKind.AssertText or ActionKind.AssertVisible).ToList();

        return
        [
            .. results.Where(result => !checks.Any(check => check.Kind == ActionKind.AssertText
                ? Same(result.Text, check.Text)
                : Same(result.Text, check.Target?.Value))),
        ];
    }

    /// <summary>
    /// The evidence for each action of a plan, in order: <c>null</c> for an action that checks nothing. A check that is
    /// both observed and stated is observed.
    /// </summary>
    public static IReadOnlyList<CheckEvidence?> EvidenceFor(
        IReadOnlyList<PlannedAction> actions,
        SiteSnapshot? site,
        IReadOnlyList<ExpectedResult>? results)
    {
        ArgumentNullException.ThrowIfNull(actions);

        return
        [
            .. actions.Select(action => action.IsAssertion
                ? Observed(action, site) ? CheckEvidence.Observed
                    : States(action, results) ? CheckEvidence.StatedByTester
                    : CheckEvidence.Assumed
                : (CheckEvidence?)null),
        ];
    }

    private static bool Observed(PlannedAction action, SiteSnapshot? site)
    {
        if (site is null)
        {
            return false;
        }

        if (action.Kind == ActionKind.AssertUrl)
        {
            return action.Path is { Length: > 0 } path
                && site.Pages.Any(page => Same(PathOnly(page.Path), PathOnly(path)));
        }

        if (action.Target is not { } target || !site.Offers(target))
        {
            return false;
        }

        // A text check is observed only when the crawl read that very text on that element.
        return action.Kind != ActionKind.AssertText
            || site.ElementsFor(target).Any(element => Same(element.Text ?? element.Name, action.Text) || Same(element.Name, action.Text));
    }

    private static string PathOnly(string path) => path.Split('?', 2)[0];

    private static bool Same(string? left, string? right) =>
        left is not null && right is not null
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool HasControl(string text) => text.Any(char.IsControl);
}
