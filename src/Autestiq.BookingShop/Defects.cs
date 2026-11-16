namespace Autestiq.BookingShop;

/// <summary>
/// The defects this clinic can be made to have, one at a time or all at once.
/// </summary>
/// <remarks>
/// Each one is a real wrong behaviour on a real page, not a flag the test can read. Like the demo
/// shop's, the benchmark runs every scenario twice: once with its defect off, where passing is
/// correct, and once with it on, where passing is a false positive.
/// </remarks>
public static class Defects
{
    private static readonly HashSet<string> Switched = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>A slot that is already booked can be booked again.</summary>
    public const string DoubleBooking = "double-booking";

    /// <summary>The confirmation shows the appointment an hour earlier than it was booked.</summary>
    public const string TimeShifted = "time-shifted";

    /// <summary>Cancelling one booking cancels the first booking made with the same email address.</summary>
    public const string CancelWrong = "cancel-wrong";

    /// <summary>The package price ignores the welcome voucher.</summary>
    public const string PackageIgnoresVoucher = "package-ignores-voucher";

    /// <summary>An email address that is not one is accepted.</summary>
    public const string EmailUnchecked = "email-unchecked";

    /// <summary>A cancellation is shown as done but is never saved.</summary>
    public const string CancelNotSaved = "cancel-not-saved";

    /// <summary>Every defect, for the benchmark to iterate.</summary>
    public static IReadOnlyList<string> All { get; } =
        [DoubleBooking, TimeShifted, CancelWrong, PackageIgnoresVoucher, EmailUnchecked, CancelNotSaved];

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

    /// <summary>Reads the ones named in the environment at startup, for running the clinic by hand.</summary>
    public static void ReadEnvironment()
    {
        if (Environment.GetEnvironmentVariable("BOOKINGSHOP_DEFECTS") is { Length: > 0 } value)
        {
            Set(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
}
