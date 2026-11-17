namespace Autestiq.Contracts.Planning;

/// <summary>
/// Which steps of a plan may change the application's data, decided by software from the plan and what exploring saw
/// (ADR-0030, part 3; gap analysis G-4). A plan with any such step needs an explicit approval that says so.
/// </summary>
/// <remarks>
/// <para>
/// The rule leans one way on purpose: a step is counted as reading only when the software can see that it reads. Going
/// to an address, waiting and checking read. Typing alone sends nothing. Clicking something exploring saw as a link
/// follows it. Everything else that acts — clicking a button or anything not seen as a link, pressing a key, choosing
/// an option — may send data, and so does clicking a target exploring never saw, because nothing says what it is.
/// </para>
/// <para>
/// The model's own view is never asked: what a plan does is read from its closed vocabulary (ADR-0019 §2). What the
/// rule cannot see is a link that changes data when followed, which HTTP says should not exist. The worker's read-only
/// guard does not see it either; the approval text says so.
/// </para>
/// <para>In <c>Contracts</c>, like <see cref="PlanOutcome"/>, because the approval and the screen must agree.</para>
/// </remarks>
public static class PlanEffects
{
    /// <summary>The 1-based numbers of the steps that may change data, in order. Empty when the plan only reads.</summary>
    public static IReadOnlyList<int> StepsThatMayChangeData(IReadOnlyList<PlannedAction> plan, SiteSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return [.. plan.Select((action, index) => (action, Number: index + 1)).Where(step => MayChangeData(step.action, snapshot)).Select(step => step.Number)];
    }

    /// <summary>Whether the plan types a secret, which cannot be approved until a secret store exists (G-6).</summary>
    public static bool NeedsSecret(IReadOnlyList<PlannedAction> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Any(action => action.SecretName is { Length: > 0 });
    }

    private static bool MayChangeData(PlannedAction action, SiteSnapshot? snapshot) => action.Kind switch
    {
        ActionKind.Goto or ActionKind.WaitFor or ActionKind.Type
            or ActionKind.AssertText or ActionKind.AssertVisible or ActionKind.AssertNotVisible or ActionKind.AssertUrl => false,
        ActionKind.Click => !IsALink(action.Target, snapshot),
        _ => true,
    };

    private static bool IsALink(ActionTarget? target, SiteSnapshot? snapshot)
    {
        if (target is null || snapshot is null)
        {
            return false;
        }

        // Every element the target could mean must be a link; one that might be a button is enough to count.
        var elements = snapshot.ElementsFor(target);

        return elements.Count > 0 && elements.All(element => string.Equals(element.Role, "link", StringComparison.OrdinalIgnoreCase));
    }
}
