using Autestiq.Contracts.Planning;

namespace Autestiq.Worker;

/// <summary>
/// Finds the checks in a plan that look for a value of the test account the plan itself typed (ADR-0033).
/// </summary>
/// <remarks>
/// <para>
/// "The page now shows demouser@…" after typing demouser@… into a sign-in form proves less than it seems. The value
/// came from the test, not from the application, and an application can show it whether or not the sign-in worked:
/// eShopOnWeb prints its demo account on its own login page, so such a check passes on a sign-in that failed. That
/// is a false positive, the one mistake this product exists not to make.
/// </para>
/// <para>
/// So a pass that rests on such a check is not counted as a pass: the run ends inconclusive, saying why, by the
/// secret's name. A failure is left as it is, because a check that fails has still found something. Only checks that
/// something is there are looked at (text, visible); a check that something is <em>not</em> there cannot pass for
/// this reason.
/// </para>
/// <para>
/// A value counts as typed when the plan types it by the secret's name, or types the same text plainly, as a planner
/// that read the value in the tester's description may. Values shorter than
/// <see cref="SecretRedaction.ShortestRedacted"/> characters are ignored, as they are when exploring.
/// </para>
/// </remarks>
internal static class TypedSecretChecks
{
    /// <summary>The names of the secrets whose typed values a positive check looks for, in plan order, each once.</summary>
    public static IReadOnlyList<string> In(IReadOnlyList<PlannedAction> plan, IReadOnlyDictionary<string, string>? secrets)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (secrets is not { Count: > 0 })
        {
            return [];
        }

        var typed = secrets
            .Where(secret => secret.Value.Length >= SecretRedaction.ShortestRedacted)
            .Where(secret => plan.Any(action => action.Kind == ActionKind.Type
                && (action.SecretName == secret.Key
                    || string.Equals(action.Text, secret.Value, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var names = new List<string>();

        foreach (var action in plan.Where(action => action.Kind is ActionKind.AssertText or ActionKind.AssertVisible))
        {
            string?[] said = [action.Text, action.Target?.Value, action.Target?.Within];

            foreach (var (name, value) in typed)
            {
                if (!names.Contains(name, StringComparer.Ordinal)
                    && said.Any(text => text is not null && text.Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    /// <summary>Why such a pass is not one. Names only, never a value.</summary>
    public static string Explain(IReadOnlyList<string> names) =>
        "Inconclusive: every step passed, but a check looks for the test account's own "
        + string.Join(" and ", names.Select(name => $"'{name}'"))
        + ", which the test typed. A page can show it whether or not signing in worked, so this pass proves nothing. "
        + "Check something only the signed-in application shows instead.";
}
