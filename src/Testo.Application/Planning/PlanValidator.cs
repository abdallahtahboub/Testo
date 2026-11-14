using Testo.Contracts.Planning;

namespace Testo.Application.Planning;

/// <summary>What validation made of a plan: what forbids it, and what is merely worth saying.</summary>
/// <param name="Errors">
/// Resource keys. Any one of these and the plan does not run at all — not in part, not repaired.
/// </param>
/// <param name="Notes">
/// Things a person should know about a plan that is allowed to run, in plain words. Recorded on
/// the run; never acted on.
/// </param>
public sealed record PlanReview(IReadOnlyList<string> Errors, IReadOnlyList<string> Notes)
{
    /// <summary>Whether the plan may run.</summary>
    public bool Allowed => Errors.Count == 0;
}

/// <summary>
/// Judges a plan before it is allowed near a browser.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0019 §3 lives here. A plan that fails is not repaired, not trimmed to its valid prefix and
/// not run in part: it goes back once with its errors, and a second failure ends the run saying
/// planning failed. "Run the bit that works" is how a product starts reporting results for tests
/// it never carried out.
/// </para>
/// <para>
/// The checks are in two tiers, and which tier a check belongs in follows from what it protects.
/// <strong>Refusals</strong> are the things that are not the application's business to decide: an
/// action that does not exist, a path that could name another origin, a plan longer than the
/// budget, a plan that checks nothing. <strong>Notes</strong> are the things a crawl cannot know:
/// above all, whether an element exists, because a crawl sees an application in one state and a
/// test exists to put it in others. A target nobody has seen is not a security problem — it fails
/// at the step, by name, with a screenshot — so refusing it would forbid most real tests to buy
/// nothing. What stops a hostile page is the closed vocabulary and the proxy, neither of which
/// depends on this (<c>docs/security-register.md</c> R-9).
/// </para>
/// </remarks>
public static class PlanValidator
{
    /// <summary>Judges a plan against the schema, the budget and the application as it was found.</summary>
    public static PlanReview Review(TestPlan? plan, SiteSnapshot site, int maxActions)
    {
        ArgumentNullException.ThrowIfNull(site);

        if (plan is null || plan.Actions.Count == 0)
        {
            return new PlanReview([PlanningErrors.Unreadable], []);
        }

        var errors = new List<string>();
        var notes = new List<string>();

        if (plan.Actions.Count > maxActions)
        {
            errors.Add(PlanningErrors.TooManyActions);
        }

        // A plan that checks nothing cannot find a defect, and a run that checks nothing and then
        // passes is worse than no run at all: it is a false assurance (ADR-0019 §7).
        if (plan.AssertionCount == 0)
        {
            errors.Add(PlanningErrors.NoAssertions);
        }

        foreach (var action in plan.Actions)
        {
            errors.AddRange(Refusals(action));

            if (Unseen(action, site) is { } note)
            {
                notes.Add(note);
            }
        }

        return new PlanReview(
            [.. errors.Distinct(StringComparer.Ordinal)],
            [.. notes.Distinct(StringComparer.Ordinal)]);
    }

    private static IEnumerable<string> Refusals(PlannedAction action)
    {
        if (!Enum.IsDefined(action.Kind))
        {
            // Unreachable through the type; reachable by deserializing a number nobody defined.
            yield return PlanningErrors.UnknownAction;
            yield break;
        }

        if (string.IsNullOrWhiteSpace(action.Description))
        {
            yield return PlanningErrors.Incomplete;
        }

        switch (action.Kind)
        {
            case ActionKind.Goto:
            case ActionKind.AssertUrl:
                foreach (var error in CheckPath(action))
                {
                    yield return error;
                }

                break;

            case ActionKind.Type:
                if (action.Text is null or "" && action.SecretName is null or "")
                {
                    yield return PlanningErrors.Incomplete;
                }

                foreach (var error in CheckTarget(action))
                {
                    yield return error;
                }

                break;

            case ActionKind.Press:
            case ActionKind.AssertText:
                if (action.Text is null or "")
                {
                    yield return PlanningErrors.Incomplete;
                }

                foreach (var error in CheckTarget(action))
                {
                    yield return error;
                }

                break;

            default:
                foreach (var error in CheckTarget(action))
                {
                    yield return error;
                }

                break;
        }
    }

    /// <summary>
    /// A path, and only a path.
    /// </summary>
    /// <remarks>
    /// Refused here as well as by the proxy, which is deliberate duplication: this one can explain
    /// itself to a person before anything runs, and that one holds when every explanation has
    /// failed. <c>//host</c>, <c>https://host</c>, a backslash and a userinfo <c>@</c> are all ways
    /// a string that looks like a path names somewhere else.
    /// </remarks>
    private static IEnumerable<string> CheckPath(PlannedAction action)
    {
        if (action.Path is not { Length: > 0 } path)
        {
            yield return PlanningErrors.Incomplete;
            yield break;
        }

        if (!path.StartsWith('/')
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.Contains("://", StringComparison.Ordinal)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains('@', StringComparison.Ordinal))
        {
            yield return PlanningErrors.ForeignOrigin;
        }
    }

    /// <summary>Whether the action says what to act on at all, which is not the same as whether it exists.</summary>
    private static IEnumerable<string> CheckTarget(PlannedAction action)
    {
        if (action.Target is not { } target || string.IsNullOrWhiteSpace(target.Value))
        {
            yield return PlanningErrors.Incomplete;
            yield break;
        }

        if (!Enum.IsDefined(target.Kind))
        {
            yield return PlanningErrors.UnknownTarget;
            yield break;
        }

        if (target.Kind == TargetKind.RoleAndName && string.IsNullOrWhiteSpace(target.Role))
        {
            yield return PlanningErrors.Incomplete;
        }
    }

    /// <summary>
    /// Says so when a plan names something the crawl never saw. Not a refusal: it may be an element
    /// that only exists once the test has done something, which is the normal case. It is still
    /// worth a person's attention, because the other explanation is a plan acting on hope.
    /// </summary>
    private static string? Unseen(PlannedAction action, SiteSnapshot site)
    {
        if (action.Target is not { } target || string.IsNullOrWhiteSpace(target.Value) || site.Offers(target))
        {
            return null;
        }

        if (action.Kind == ActionKind.AssertNotVisible)
        {
            // An absence the crawl also did not see is the ordinary case for this action, and
            // saying so on every run would teach a person to skip the notes.
            return null;
        }

        return $"The step “{action.Description}” acts on {target}, which was not on any page "
            + "the crawl saw. That is normal for something that appears only once the test has acted, "
            + "and a mistake otherwise.";
    }
}
