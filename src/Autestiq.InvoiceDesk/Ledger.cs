using System.Globalization;
using System.Text;

namespace Autestiq.InvoiceDesk;

/// <summary>A customer of the business.</summary>
public sealed record Client(string Name, string Email)
{
    /// <summary>The name as it appears in a test id: lower case, with anything but letters and digits as one dash.</summary>
    public string Slug => Ledger.Slug(Name);
}

/// <summary>An invoice: one line of work, its VAT, and where it stands.</summary>
public sealed class Invoice
{
    public required string Number { get; init; }
    public required string Client { get; init; }
    public required DateOnly Issued { get; init; }
    public required string Description { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required string Status { get; set; }
    public DateOnly? PaidOn { get; set; }

    /// <summary>What the work comes to before VAT.</summary>
    public decimal Subtotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>The VAT on it. With the rounding defect, rounded down instead of to the nearest penny.</summary>
    public decimal Vat => Defects.On(Defects.VatRoundedDown)
        ? decimal.Floor(Subtotal * Ledger.VatRate * 100) / 100
        : decimal.Round(Subtotal * Ledger.VatRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>What is owed.</summary>
    public decimal Total => Subtotal + Vat;

    /// <summary>Whether it can still be paid, and chased.</summary>
    public bool IsOpen => Status is "Sent" or "Overdue";
}

/// <summary>
/// The business's clients and invoices, and the arithmetic. Seeded the same way every time, and put back
/// to the seed by <see cref="Reset"/>, which the benchmark calls before every run.
/// </summary>
public static class Ledger
{
    /// <summary>The VAT rate.</summary>
    public const decimal VatRate = 0.20m;

    /// <summary>How many invoices a page lists.</summary>
    public const int PageSize = 10;

    /// <summary>The desk's idea of today, fixed so that "this month" means the same thing on every run.</summary>
    public static DateOnly Today { get; } = new(2026, 11, 15);

    private static readonly Lock Gate = new();
    private static List<Client> _clients = Seed.Clients();
    private static List<Invoice> _invoices = Seed.Invoices();

    /// <summary>The clients, in the order they were added.</summary>
    public static IReadOnlyList<Client> Clients
    {
        get
        {
            lock (Gate)
            {
                return [.. _clients];
            }
        }
    }

    /// <summary>The invoices, by number.</summary>
    public static IReadOnlyList<Invoice> Invoices
    {
        get
        {
            lock (Gate)
            {
                return [.. _invoices];
            }
        }
    }

    /// <summary>Puts everything back as it was seeded.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _clients = Seed.Clients();
            _invoices = Seed.Invoices();
        }
    }

    /// <summary>Finds an invoice by its number, or null.</summary>
    public static Invoice? Find(string? number) =>
        Invoices.FirstOrDefault(invoice => string.Equals(invoice.Number, number?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a client by name, ignoring case, or null.</summary>
    public static Client? FindClient(string? name) =>
        Clients.FirstOrDefault(client => string.Equals(client.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The invoices to list: filtered by status and client, sorted, and cut to one page. With the filter
    /// defect the status is ignored; with the paging defect every page after the first starts one late.
    /// </summary>
    public static (IReadOnlyList<Invoice> Page, int Matching, int Pages) List(string? status, string? client, string? sort, int page)
    {
        IEnumerable<Invoice> matching = Invoices;

        if (status is { Length: > 0 } && status != "all" && !Defects.On(Defects.FilterIgnored))
        {
            matching = matching.Where(invoice => string.Equals(invoice.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (client is { Length: > 0 })
        {
            matching = matching.Where(invoice => invoice.Client.Contains(client.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (sort == "total")
        {
            matching = matching.OrderByDescending(invoice => invoice.Total).ThenBy(invoice => invoice.Number, StringComparer.Ordinal);
        }

        var all = matching.ToList();
        var pages = Math.Max(1, (all.Count + PageSize - 1) / PageSize);
        var current = Math.Clamp(page, 1, pages);
        var skip = (current - 1) * PageSize + (current > 1 && Defects.On(Defects.PageSkipsOne) ? 1 : 0);

        return ([.. all.Skip(skip).Take(PageSize)], all.Count, pages);
    }

    /// <summary>
    /// Marks an invoice as paid today. With the defect, the first open invoice is marked instead.
    /// </summary>
    public static void MarkPaid(Invoice requested)
    {
        lock (Gate)
        {
            var target = Defects.On(Defects.PaidMarksAnother)
                ? _invoices.FirstOrDefault(invoice => invoice.IsOpen) ?? requested
                : requested;

            if (target.IsOpen)
            {
                target.Status = "Paid";
                target.PaidOn = Today;
            }
        }
    }

    /// <summary>Adds a draft invoice and returns it.</summary>
    public static Invoice Create(Client client, string description, int quantity, decimal unitPrice)
    {
        lock (Gate)
        {
            var next = 1001 + _invoices.Count;
            var invoice = new Invoice
            {
                Number = $"INV-{next}",
                Client = client.Name,
                Issued = Today,
                Description = description,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Status = "Draft",
            };

            _invoices.Add(invoice);
            return invoice;
        }
    }

    /// <summary>Adds a client.</summary>
    public static void AddClient(Client client)
    {
        lock (Gate)
        {
            _clients.Add(client);
        }
    }

    /// <summary>What is owed on invoices that have been sent. With the defect, paid invoices are counted too.</summary>
    public static decimal Outstanding => Invoices
        .Where(invoice => invoice.IsOpen || (Defects.On(Defects.OutstandingIncludesPaid) && invoice.Status == "Paid"))
        .Sum(invoice => invoice.Total);

    /// <summary>What was paid in the current month.</summary>
    public static decimal PaidThisMonth => Invoices
        .Where(invoice => invoice.PaidOn is { } paid && paid.Year == Today.Year && paid.Month == Today.Month)
        .Sum(invoice => invoice.Total);

    /// <summary>How many invoices are overdue.</summary>
    public static int OverdueCount => Invoices.Count(invoice => invoice.Status == "Overdue");

    /// <summary>Money, as the desk shows it.</summary>
    public static string Money(decimal amount) =>
        "£" + amount.ToString("#,0.00", CultureInfo.InvariantCulture);

    /// <summary>A date as a person reads it, for example "15 Nov 2026".</summary>
    public static string DateText(DateOnly date) =>
        date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A name as part of a test id.</summary>
    public static string Slug(string name)
    {
        var slug = new StringBuilder();

        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-');
    }

    /// <summary>The seed: six clients and twenty-five invoices, the same every time.</summary>
    private static class Seed
    {
        public static List<Client> Clients() =>
        [
            new("Harbour Coffee", "accounts@harbour-coffee.example"),
            new("Northgate Studio", "studio@northgate.example"),
            new("Pine & Co", "hello@pineandco.example"),
            new("Riverside Physio", "office@riverside-physio.example"),
            new("Bluebell Bakery", "orders@bluebell.example"),
            new("Lantern Books", "shop@lanternbooks.example"),
        ];

        public static List<Invoice> Invoices() =>
        [
            Make(1001, "Harbour Coffee", "2026-09-02", "Espresso machine service", 1, 180.00m, "Paid", "2026-09-20"),
            Make(1002, "Northgate Studio", "2026-09-05", "Brand workshop", 2, 450.00m, "Paid", "2026-10-01"),
            Make(1003, "Pine & Co", "2026-09-12", "Shelving installation", 3, 85.50m, "Paid", "2026-10-03"),
            Make(1004, "Riverside Physio", "2026-09-18", "Website hosting (year)", 1, 240.00m, "Overdue"),
            Make(1005, "Bluebell Bakery", "2026-09-22", "Logo redesign", 1, 320.00m, "Sent"),
            Make(1006, "Lantern Books", "2026-09-30", "Bookshop signage", 2, 125.00m, "Paid", "2026-11-03"),
            Make(1007, "Harbour Coffee", "2026-10-01", "Barista training", 4, 60.00m, "Overdue"),
            Make(1008, "Northgate Studio", "2026-10-04", "Photography day", 1, 600.00m, "Sent"),
            Make(1009, "Pine & Co", "2026-10-08", "Workbench repair", 1, 12.34m, "Draft"),
            Make(1010, "Riverside Physio", "2026-10-10", "Appointment reminders setup", 1, 95.00m, "Paid", "2026-11-10"),
            Make(1011, "Bluebell Bakery", "2026-10-14", "Menu boards", 3, 45.00m, "Sent"),
            Make(1012, "Lantern Books", "2026-10-17", "Event posters", 50, 1.20m, "Paid", "2026-11-12"),
            Make(1013, "Harbour Coffee", "2026-10-21", "Grinder parts", 2, 37.25m, "Draft"),
            Make(1014, "Northgate Studio", "2026-10-24", "Studio lighting hire", 3, 75.00m, "Overdue"),
            Make(1015, "Pine & Co", "2026-10-28", "Delivery van livery", 1, 410.00m, "Sent"),
            Make(1016, "Riverside Physio", "2026-11-01", "Leaflet print run", 500, 0.18m, "Draft"),
            Make(1017, "Bluebell Bakery", "2026-11-02", "Packaging design", 1, 275.00m, "Sent"),
            Make(1018, "Lantern Books", "2026-11-03", "Window display", 1, 150.00m, "Draft"),
            Make(1019, "Harbour Coffee", "2026-11-05", "Loyalty card print", 1000, 0.07m, "Sent"),
            Make(1020, "Northgate Studio", "2026-11-06", "Editing", 6, 55.00m, "Paid", "2026-11-14"),
            Make(1021, "Pine & Co", "2026-11-07", "Site visit", 1, 65.00m, "Sent"),
            Make(1022, "Riverside Physio", "2026-11-09", "Social media pack", 1, 180.00m, "Draft"),
            Make(1023, "Bluebell Bakery", "2026-11-10", "Cake stand hire", 4, 12.50m, "Sent"),
            Make(1024, "Lantern Books", "2026-11-12", "Author evening tickets", 30, 3.50m, "Sent"),
            Make(1025, "Harbour Coffee", "2026-11-14", "Coffee bean labels", 200, 0.45m, "Draft"),
        ];

        private static Invoice Make(int number, string client, string issued, string description, int quantity, decimal unit, string status, string? paid = null) => new()
        {
            Number = $"INV-{number}",
            Client = client,
            Issued = DateOnly.ParseExact(issued, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Description = description,
            Quantity = quantity,
            UnitPrice = unit,
            Status = status,
            PaidOn = paid is null ? null : DateOnly.ParseExact(paid, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }
}
