namespace Testo.DemoShop;

/// <summary>
/// The defects this shop can be made to have, one at a time or all at once.
/// </summary>
/// <remarks>
/// <para>
/// This is the instrument the whole AI benchmark rests on. "Pass rate" alone says nothing useful:
/// a tester that always says "passed" scores 100% against a working application and is worthless.
/// What matters is whether it says "passed" while the application is broken, and that can only be
/// measured against an application whose breakage we control.
/// </para>
/// <para>
/// So each defect here is real — a wrong number on a real page, not a flag the test can read — and
/// the benchmark runs every scenario twice: once with its defect off, where a pass is correct, and
/// once with it on, where a pass is a <strong>false positive</strong> and the number that decides
/// whether this product can be believed.
/// </para>
/// </remarks>
public static class Defects
{
    private static readonly HashSet<string> Switched = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>The checkout charges for the first item in the basket and forgets the rest.</summary>
    public const string CheckoutTotal = "checkout-total";

    /// <summary>The basket counts each line once, however many of that item there are.</summary>
    public const string QuantityIgnored = "quantity-ignored";

    /// <summary>A discount code is taken off twice.</summary>
    public const string DiscountTwice = "discount-twice";

    /// <summary>Any password is accepted, as long as the account exists.</summary>
    public const string SignInAnyPassword = "signin-any-password";

    /// <summary>Removing a line removes the first one instead of the one asked for.</summary>
    public const string RemoveWrongItem = "remove-wrong-item";

    /// <summary>An empty basket can still be ordered, for nothing.</summary>
    public const string EmptyOrderAllowed = "empty-order-allowed";

    /// <summary>Every defect, for the benchmark to iterate.</summary>
    public static IReadOnlyList<string> All { get; } =
        [CheckoutTotal, QuantityIgnored, DiscountTwice, SignInAnyPassword, RemoveWrongItem, EmptyOrderAllowed];

    /// <summary>Whether this defect is switched on.</summary>
    public static bool On(string defect)
    {
        lock (Gate)
        {
            return Switched.Contains(defect);
        }
    }

    /// <summary>The ones currently switched on.</summary>
    public static IReadOnlyList<string> Current
    {
        get
        {
            lock (Gate)
            {
                return [.. Switched.Order(StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>
    /// Switches exactly these on and everything else off. Unknown names are refused, so a typo in
    /// a benchmark configuration cannot silently mean "no defects" and flatter the results.
    /// </summary>
    public static bool Set(IEnumerable<string> defects)
    {
        ArgumentNullException.ThrowIfNull(defects);

        var wanted = defects
            .Select(defect => defect.Trim())
            .Where(defect => defect.Length > 0)
            .ToList();

        if (wanted.Any(defect => !All.Contains(defect, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        lock (Gate)
        {
            Switched.Clear();

            foreach (var defect in wanted)
            {
                Switched.Add(defect);
            }
        }

        return true;
    }

    /// <summary>Reads the ones named in the environment at startup, for running the shop by hand.</summary>
    public static void ReadEnvironment()
    {
        if (Environment.GetEnvironmentVariable("DEMOSHOP_DEFECTS") is { Length: > 0 } value)
        {
            Set(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
}
