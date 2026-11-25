using Autestiq.Contracts.Planning;

namespace Autestiq.Worker;

/// <summary>
/// Takes the test account's values out of what an exploration found, before the description leaves the worker
/// (ADR-0033).
/// </summary>
/// <remarks>
/// <para>
/// The description goes to the planner, which is a model, and credentials never go to a model. A signed-in page
/// often shows the user name ("signed in as …"), and a careless application may echo even a password. Leaving
/// that to the planner's good manners would be enforcing by prompt; this is the software doing it.
/// </para>
/// <para>
/// Each value is replaced, wherever it appears and in any letter case, by a marker naming the secret, so the
/// planner still learns that the account's user name is shown there. Values shorter than
/// <see cref="ShortestRedacted"/> characters are not replaced: a one-letter value would take every such letter
/// out of the page. The store asks for values of real length; a test account with a three-letter password is
/// the customer's own choice, and stated.
/// </para>
/// </remarks>
internal static class SecretRedaction
{
    /// <summary>The shortest value replaced.</summary>
    public const int ShortestRedacted = TypedSecretChecks.ShortestValue;

    /// <summary>What stands in for a value: the secret's name, never its value.</summary>
    public static string Marker(string name) => $"[secret:{name}]";

    /// <summary>The snapshot with every secret value replaced by its marker.</summary>
    public static SiteSnapshot Without(SiteSnapshot snapshot, IReadOnlyDictionary<string, string>? secrets)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Longest first, so a value that contains another is replaced whole.
        var values = (secrets ?? new Dictionary<string, string>())
            .Where(secret => secret.Value.Length >= ShortestRedacted)
            .OrderByDescending(secret => secret.Value.Length)
            .ThenBy(secret => secret.Key, StringComparer.Ordinal)
            .ToList();

        if (values.Count == 0)
        {
            return snapshot;
        }

        string Clean(string text)
        {
            foreach (var (name, value) in values)
            {
                text = text.Replace(value, Marker(name), StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }

        string? CleanOrNull(string? text) => text is null ? null : Clean(text);

        return snapshot with
        {
            Pages =
            [
                .. snapshot.Pages.Select(page => page with
                {
                    Path = Clean(page.Path),
                    Title = Clean(page.Title),
                    Heading = CleanOrNull(page.Heading),
                    ReachedBy = page.ReachedBy is null ? null : [.. page.ReachedBy.Select(Clean)],
                    Elements =
                    [
                        .. page.Elements.Select(element => element with
                        {
                            Name = Clean(element.Name),
                            TestId = CleanOrNull(element.TestId),
                            Text = CleanOrNull(element.Text),
                            Within = CleanOrNull(element.Within),
                        }),
                    ],
                }),
            ],
        };
    }
}
