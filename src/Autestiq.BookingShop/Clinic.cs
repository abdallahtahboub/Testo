using System.Globalization;

namespace Autestiq.BookingShop;

/// <summary>A treatment the clinic offers.</summary>
public sealed record Service(string Id, string Name, decimal Price, string Description);

/// <summary>A booked appointment.</summary>
public sealed class Booking(string reference, Service service, DateOnly date, TimeOnly time, string name, string email, string notes, decimal total)
{
    public string Reference { get; } = reference;
    public Service Service { get; } = service;
    public DateOnly Date { get; } = date;
    public TimeOnly Time { get; } = time;
    public string Name { get; } = name;
    public string Email { get; } = email;
    public string Notes { get; } = notes;
    public decimal Total { get; } = total;
    public bool Cancelled { get; set; }
}

/// <summary>
/// The clinic's services, its diary and its arithmetic. One practitioner, so a slot can be booked
/// once, for any service.
/// </summary>
public static class Clinic
{
    /// <summary>The welcome voucher, for 20% off.</summary>
    public const string Voucher = "WELCOME20";

    /// <summary>The fraction taken off with the voucher.</summary>
    public const decimal VoucherRate = 0.20m;

    private static readonly Lock Gate = new();
    private static readonly List<Booking> Diary = [];
    private static int _lastNumber = 1000;

    /// <summary>The services, in the order the clinic lists them.</summary>
    public static IReadOnlyList<Service> Services { get; } =
    [
        new("initial", "Initial assessment", 60.00m, "A full assessment of the problem, with a plan to fix it."),
        new("follow", "Follow-up appointment", 45.00m, "A check on progress and an adjustment to the plan."),
        new("massage", "Sports massage", 50.00m, "Deep tissue work for training and recovery."),
        new("package", "Back pain course (6 sessions)", 270.00m, "Six sessions over six weeks, paid once."),
    ];

    /// <summary>The days the clinic is open for booking, starting from a fixed week for repeatable runs.</summary>
    public static IReadOnlyList<DateOnly> OpenDays { get; } =
        [.. Enumerable.Range(0, 5).Select(offset => new DateOnly(2026, 11, 16).AddDays(offset))];

    /// <summary>The start times offered each day.</summary>
    public static IReadOnlyList<TimeOnly> Times { get; } =
    [
        new(9, 0), new(10, 0), new(11, 0), new(14, 0), new(15, 0),
    ];

    /// <summary>Finds a service by its id, or null.</summary>
    public static Service? Find(string? id) =>
        Services.FirstOrDefault(service => string.Equals(service.Id, id, StringComparison.Ordinal));

    /// <summary>Whether a slot is booked by anybody, for any service.</summary>
    public static bool IsBooked(DateOnly date, TimeOnly time)
    {
        lock (Gate)
        {
            return Diary.Any(booking => !booking.Cancelled && booking.Date == date && booking.Time == time);
        }
    }

    /// <summary>
    /// The price after the voucher. A package ignores it when the package defect is on, which is the
    /// bug the benchmark is looking for.
    /// </summary>
    public static decimal PriceFor(Service service, string? voucher)
    {
        var welcome = string.Equals(voucher?.Trim(), Voucher, StringComparison.OrdinalIgnoreCase);
        var ignored = service.Id == "package" && Defects.On(Defects.PackageIgnoresVoucher);

        return welcome && !ignored
            ? service.Price * (1 - VoucherRate)
            : service.Price;
    }

    /// <summary>Records a booking and returns it.</summary>
    public static Booking Add(Service service, DateOnly date, TimeOnly time, string name, string email, string notes, string? voucher)
    {
        lock (Gate)
        {
            _lastNumber++;
            var booking = new Booking($"BK-{_lastNumber}", service, date, time, name, email, notes, PriceFor(service, voucher));
            Diary.Add(booking);
            return booking;
        }
    }

    /// <summary>Finds a booking by its reference, or null.</summary>
    public static Booking? FindByReference(string? reference)
    {
        lock (Gate)
        {
            return Diary.FirstOrDefault(booking => string.Equals(booking.Reference, reference?.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Cancels a booking. With the cancel defect on, the first live booking made with the same email is
    /// cancelled instead. With the not-saved defect on, nothing is recorded at all.
    /// </summary>
    public static void Cancel(Booking requested)
    {
        lock (Gate)
        {
            if (Defects.On(Defects.CancelNotSaved))
            {
                return;
            }

            var target = Defects.On(Defects.CancelWrong)
                ? Diary.FirstOrDefault(booking => !booking.Cancelled && string.Equals(booking.Email, requested.Email, StringComparison.OrdinalIgnoreCase))
                    ?? requested
                : requested;

            target.Cancelled = true;
        }
    }

    /// <summary>Starts the diary afresh. The benchmark calls this before each run, so that one run's bookings cannot take a slot from the next.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            Diary.Clear();
            _lastNumber = 1000;
        }
    }

    /// <summary>A day as a person reads it, for example "Monday 16 November 2026".</summary>
    public static string DayText(DateOnly date) =>
        date.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A time as a person reads it, for example "09:00".</summary>
    public static string TimeText(TimeOnly time) =>
        time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Money, as the clinic shows it.</summary>
    public static string Money(decimal amount) =>
        "£" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
