namespace Autestiq.InvoiceDesk;

/// <summary>
/// The defects this invoice desk can be made to have, one at a time or all at once.
/// </summary>
/// <remarks>
/// Each one is a real wrong behaviour on a real page, not a flag the test can read. The benchmark runs
/// every scenario twice: once with its defect off, where passing is correct, and once with it on, where
/// passing is a false positive.
/// </remarks>
public static class Defects
{
    private static readonly HashSet<string> Switched = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>VAT is rounded down to the penny instead of to the nearest penny.</summary>
    public const string VatRoundedDown = "vat-rounded-down";

    /// <summary>The status filter is ignored, so every invoice is listed whatever is chosen.</summary>
    public const string FilterIgnored = "filter-ignored";

    /// <summary>Every page after the first starts one invoice late, so one invoice is never listed.</summary>
    public const string PageSkipsOne = "page-skips-one";

    /// <summary>Marking an invoice as paid marks the first unpaid invoice instead.</summary>
    public const string PaidMarksAnother = "paid-marks-another";

    /// <summary>The outstanding total counts paid invoices too.</summary>
    public const string OutstandingIncludesPaid = "outstanding-includes-paid";

    /// <summary>A client's email address is not checked.</summary>
    public const string ClientEmailUnchecked = "client-email-unchecked";

    /// <summary>Every defect, for the benchmark to iterate.</summary>
    public static IReadOnlyList<string> All { get; } =
        [VatRoundedDown, FilterIgnored, PageSkipsOne, PaidMarksAnother, OutstandingIncludesPaid, ClientEmailUnchecked];

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
    /// Switches exactly these on and everything else off. Unknown names are refused, so a typo in a
    /// benchmark configuration cannot silently mean "no defects" and flatter the results.
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

    /// <summary>Reads the ones named in the environment at startup, for running the desk by hand.</summary>
    public static void ReadEnvironment()
    {
        if (Environment.GetEnvironmentVariable("INVOICEDESK_DEFECTS") is { Length: > 0 } value)
        {
            Set(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
}
