namespace Autestiq.Benchmark;

/// <summary>
/// Changes an application can have between the time a test was planned and the time it runs, as a new version of
/// it would (Experiment 0001, <c>docs/ai-learning/experiments/0001-repair-planning-from-verified-experience.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// Unlike a defect, a change is not a fault in the application: its behaviour is the same, only its markup moved on.
/// A test written against the earlier version may fail on it, and a good repair passes it again, while still failing
/// any defect the application has. That difference is the experiment's ground truth.
/// </para>
/// <para>
/// Applied by the in-process host as it serves a page, so our three applications need no change of their own, and
/// only to controls and test ids, never to the values a test checks. Lab only: nothing in the product sets it.
/// </para>
/// </remarks>
public static class AppChanges
{
    /// <summary>Every <c>data-testid</c> gets a new value, as when a team renames its test ids.</summary>
    public const string Retag = "retag";

    /// <summary>Buttons and links get new words for the same actions, as in a copy revision.</summary>
    public const string Relabel = "relabel";

    /// <summary>The changes there are.</summary>
    public static IReadOnlyList<string> All { get; } = [Retag, Relabel];

    private static readonly Lock Gate = new();
    private static string? _current;

    /// <summary>The change in force, or <c>null</c> for the version the tests were written against.</summary>
    public static string? Current
    {
        get { lock (Gate) { return _current; } }
        set
        {
            if (value is not null && !All.Contains(value, StringComparer.Ordinal))
            {
                throw new ArgumentException($"'{value}' is not a change this lab knows.", nameof(value));
            }

            lock (Gate) { _current = value; }
        }
    }

    /// <summary>The new words for each target's controls, chosen from its source: controls only, never checked values.</summary>
    public static IReadOnlyDictionary<string, string> LabelsFor(string target) => target switch
    {
        "booking" => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Book this"] = "Choose",
            ["Confirm booking"] = "Book now",
            ["Find my booking"] = "Look up booking",
            ["Cancel this booking"] = "Cancel booking",
            ["Book an appointment"] = "Make an appointment",
        },
        "invoices" => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Create invoice"] = "Save invoice",
            ["Mark as paid"] = "Record payment",
            ["Send reminder"] = "Remind client",
            ["Add client"] = "New client",
            ["Search"] = "Find",
            ["Next"] = "Next page",
            ["Largest first"] = "Sort by amount",
        },
        _ => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Add to basket"] = "Add to cart",
            ["Checkout"] = "Go to checkout",
            ["Apply"] = "Apply code",
            ["Remove"] = "Delete",
            ["Search"] = "Find",
            ["Place the order"] = "Place order",
            ["Back to the catalogue"] = "Continue shopping",
            ["Sign in"] = "Log in",
        },
    };

    /// <summary>A page as the changed version serves it.</summary>
    public static string Apply(string html, string change, IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(labels);

        if (change == Retag)
        {
            return System.Text.RegularExpressions.Regex.Replace(html, @"data-testid=""([^""]+)""", @"data-testid=""$1-v2""");
        }

        foreach (var (old, renamed) in labels)
        {
            html = html
                .Replace($">{old}<", $">{renamed}<", StringComparison.Ordinal)
                .Replace($"value=\"{old}\"", $"value=\"{renamed}\"", StringComparison.Ordinal)
                .Replace($"aria-label=\"{old}\"", $"aria-label=\"{renamed}\"", StringComparison.Ordinal);
        }

        return html;
    }
}
