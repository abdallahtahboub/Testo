using Autestiq.Contracts.Planning;
using static Autestiq.Benchmark.BenchmarkScenarios;

namespace Autestiq.Benchmark;

/// <summary>
/// The unseen set: twenty scenarios across the three applications, written on 2026-11-16 after the planner's
/// prompt was frozen (PR #46). They decide the ADR-0020 gate.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why a new set.</strong> The first held-out set (<see cref="HeldOutScenarios"/>) shaped the prompt
/// fix of 2026-11-16, so passing it shows that the fix works, not that the planner works on scenarios nobody
/// has looked at. These were written without looking at how the planner handles them, aimed at what the other
/// sets do not cover: a word inside a name, a line's own total, three items at once, an order that matters
/// with two of one product, a voucher on another service, another day of the week, the paid filter, the
/// "Next" link, a new invoice's VAT, and a search in lower case.
/// </para>
/// <para>
/// <strong>Three defects no planner has seen</strong> were added with them, one per application: a basket
/// line that ignores its quantity, a follow-up charged at the initial price, and a client search that
/// minds capitals. The other ten defects are existing ones described in new words.
/// </para>
/// <para>
/// <strong>Freeze rule</strong>, as for every set: a scenario, its baseline or its defect may change only if
/// its baseline fails a sound application or passes a broken one, and only before the first planner run.
/// After that run, nothing here changes, and a fix the run suggests is measured on yet another set.
/// </para>
/// </remarks>
public static class UnseenScenarios
{
    /// <summary>The scenarios for one target: <c>demo</c>, <c>booking</c> or <c>invoices</c>.</summary>
    public static IReadOnlyList<BenchmarkScenario> For(string target) => target switch
    {
        "booking" => Clinic,
        "invoices" => Desk,
        _ => Shop,
    };

    /// <summary>The demo shop's six.</summary>
    public static IReadOnlyList<BenchmarkScenario> Shop { get; } =
    [
        new(
            "search-finds-a-word-inside-a-name",
            "Search the catalogue for 'iron' and check the cast iron teapot comes up.",
            [
                Goto("Open the catalogue", "/"),
                Type("Search for iron", "search", "iron"),
                Click("Search", "do-search"),
                AssertVisible("The teapot is found", "name-pot"),
            ]),

        new(
            "account-page-offers-a-way-in",
            "Without signing in, open the account page and check it says you are not signed in and offers a way to sign in.",
            [
                Goto("Open the account page", "/account"),
                AssertVisible("It says nobody is signed in", "not-signed-in"),
                AssertVisible("It offers a way to sign in", "to-sign-in"),
            ]),

        new(
            "three-things-at-checkout",
            "Put the tea, the cup and the tray in the basket, go to the checkout, and check it counts 3 items and asks for £42.75.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Back to the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tray", "add-tray"),
                Goto("Open the checkout", "/checkout"),
                Assert("It counts three items", "checkout-count", "3"),
                Assert("It asks for £42.75", "checkout-total", "£42.75"),
            ],
            Autestiq.DemoShop.Defects.CheckoutTotal),

        new(
            "two-cups-on-one-line",
            "Add the cup to the basket twice and check the cup's line in the basket says 2 and comes to £22.50.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Goto("Back to the catalogue", "/"),
                Click("Add the cup again", "add-cup"),
                Assert("The cup's line says 2", "quantity-cup", "2"),
                Assert("The cup's line comes to £22.50", "line-total-cup", "£22.50"),
            ],
            Autestiq.DemoShop.Defects.LineTotalIgnoresQuantity),

        new(
            "save10-on-the-teapot-and-the-tea",
            "Buy the teapot and the tea with the discount code SAVE10 and check the checkout asks for £45.45.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add the teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add the tea", "add-tea"),
                Goto("Open the checkout", "/checkout"),
                Type("Type the code", "code", "SAVE10"),
                Click("Apply it", "apply-code"),
                Assert("The checkout asks for £45.45", "checkout-total", "£45.45"),
            ],
            Autestiq.DemoShop.Defects.DiscountTwice),

        new(
            "removing-the-cup-keeps-both-teapots",
            "Add two teapots and then a cup, remove the cup, and check the basket still holds two teapots for £84.00.",
            [
                Goto("Open the catalogue", "/"),
                Click("Add a teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add a second teapot", "add-pot"),
                Goto("Back to the catalogue", "/"),
                Click("Add the cup", "add-cup"),
                Click("Remove the cup", "remove-cup"),
                AssertHidden("The cup is gone", "line-cup"),
                Assert("Two teapots are left", "quantity-pot", "2"),
                Assert("The basket comes to £84.00", "basket-total", "£84.00"),
            ],
            Autestiq.DemoShop.Defects.RemoveWrongItem),
    ];

    /// <summary>The clinic's seven. The clinic is open Monday 16 to Friday 20 November 2026.</summary>
    public static IReadOnlyList<BenchmarkScenario> Clinic { get; } =
    [
        new(
            "massage-details-give-its-price",
            "Start booking a sports massage for Wednesday 18 November at 14:00 and check the details page gives the price as £50.00 before any voucher.",
            [
                .. Reach("massage", "18", "14:00"),
                Assert("The price before any voucher is £50.00", "detail-price", "Price before any voucher: £50.00"),
            ]),

        new(
            "contact-page-gives-the-email-address",
            "Check the contact page gives the clinic's email address as hello@riverside.example.",
            [
                Goto("Open the contact page", "/contact"),
                Assert("The email address is shown", "contact-email", "hello@riverside.example"),
            ]),

        new(
            "follow-up-with-the-welcome-voucher",
            "Book a follow-up appointment for Thursday 19 November at 15:00 using the voucher WELCOME20, and check the booking's total is £36.00.",
            [
                .. Book("follow", "19", "15:00", "Grace Hopper", "grace@example.test", "WELCOME20"),
                Assert("The total is £36.00", "booking-total", "£36.00"),
            ]),

        new(
            "a-taken-hour-is-not-offered-again",
            "Book a sports massage for Tuesday 17 November at 11:00. Then start booking an initial assessment for the same day and check that 11:00 is shown as booked.",
            [
                .. Book("massage", "17", "11:00", "Grace Hopper", "grace@example.test", null),
                Goto("Open the services", "/services"),
                Click("Book the initial assessment", "book-initial"),
                Click("Choose Tuesday 17 November", "date-2026-11-17"),
                AssertVisible("11:00 is shown as booked", "booked-11"),
                AssertHidden("11:00 cannot be chosen", "time-11"),
            ],
            Autestiq.BookingShop.Defects.DoubleBooking),

        new(
            "friday-booking-keeps-its-hour",
            "Book a follow-up appointment for Friday 20 November at 15:00 and check the booking says Friday 20 November 2026 at 15:00.",
            [
                .. Book("follow", "20", "15:00", "Grace Hopper", "grace@example.test", null),
                Assert("The booking is for Friday at 15:00", "booking-when", "Friday 20 November 2026 at 15:00"),
            ],
            Autestiq.BookingShop.Defects.TimeShifted),

        new(
            "follow-up-costs-a-follow-up",
            "Book a follow-up appointment for Tuesday 17 November at 9:00 and check the booking's total is £45.00.",
            [
                .. Book("follow", "17", "09:00", "Grace Hopper", "grace@example.test", null),
                Assert("The total is £45.00", "booking-total", "£45.00"),
            ],
            Autestiq.BookingShop.Defects.FollowUpPricedAsInitial),

        new(
            "an-address-without-an-at-sign-is-refused",
            "Try to book an initial assessment for Thursday 19 November at 10:00 giving the email address 'ada.example.test', and check the clinic asks for a valid email address.",
            [
                .. Reach("initial", "19", "10:00"),
                Type("Type the name", "full-name", "Ada Lovelace"),
                Type("Type an address with no @", "email", "ada.example.test"),
                Click("Confirm the booking", "confirm-booking"),
                AssertVisible("The clinic asks for a valid email address", "error-email"),
            ],
            Autestiq.BookingShop.Defects.EmailUnchecked),
    ];

    /// <summary>The invoice desk's seven. The ledger starts from its seed before every run.</summary>
    public static IReadOnlyList<BenchmarkScenario> Desk { get; } =
    [
        new(
            "loyalty-card-invoice-adds-up",
            "Open invoice INV-1019 and check it bills a quantity of 1000 at £0.07 each, with VAT of £14.00 and a total of £84.00.",
            [
                Goto("Open the invoice", "/invoices/INV-1019"),
                Assert("The quantity is 1000", "line-quantity", "1000"),
                Assert("The unit price is £0.07", "line-unit-price", "£0.07"),
                Assert("The VAT is £14.00", "vat", "£14.00"),
                Assert("The total is £84.00", "total", "£84.00"),
            ]),

        new(
            "clients-page-gives-bluebells-address",
            "Check the clients page lists Bluebell Bakery with the email address orders@bluebell.example.",
            [
                Goto("Open the clients", "/clients"),
                Assert("Bluebell Bakery is listed", "client-name-bluebell-bakery", "Bluebell Bakery"),
                Assert("Its address is orders@bluebell.example", "client-email-bluebell-bakery", "orders@bluebell.example"),
            ]),

        new(
            "paid-filter-shows-the-seven-paid",
            "Show only the paid invoices and check there are 7 of them, and that INV-1005, which has not been paid, is not listed.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Show only the paid ones", "filter-paid"),
                Assert("Seven invoices are shown", "invoice-count", "Showing 7 of 7 invoices"),
                AssertHidden("INV-1005 is not listed", "row-INV-1005"),
            ],
            Autestiq.InvoiceDesk.Defects.FilterIgnored),

        new(
            "vat-on-a-new-small-invoice",
            "Create an invoice for Bluebell Bakery for one cake tin at £10.03 and check the VAT on it is £2.01.",
            [
                Goto("Open a new invoice", "/invoices/new"),
                Type("Type the client", "client-name", "Bluebell Bakery"),
                Type("Type the description", "description", "Cake tin"),
                Type("Type the quantity", "quantity", "1"),
                Type("Type the unit price", "unit-price", "10.03"),
                Click("Create the invoice", "create-invoice"),
                Assert("The VAT is £2.01", "vat", "£2.01"),
            ],
            Autestiq.InvoiceDesk.Defects.VatRoundedDown),

        new(
            "next-page-carries-on-from-inv-1011",
            "On the first page of invoices, follow the Next link and check the page it leads to includes INV-1011.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Follow Next", "next-page"),
                AssertVisible("INV-1011 is on that page", "row-INV-1011"),
            ],
            Autestiq.InvoiceDesk.Defects.PageSkipsOne),

        new(
            "paying-one-leaves-the-overdue-one-alone",
            "Mark invoice INV-1015 as paid, and check it now shows as Paid while INV-1004 still shows as Overdue.",
            [
                Goto("Open INV-1015", "/invoices/INV-1015"),
                Click("Mark it as paid", "mark-paid"),
                Assert("INV-1015 is Paid", "invoice-status", "Paid"),
                Goto("Open INV-1004", "/invoices/INV-1004"),
                Assert("INV-1004 is still Overdue", "invoice-status", "Overdue"),
            ],
            Autestiq.InvoiceDesk.Defects.PaidMarksAnother),

        new(
            "client-search-in-lower-case",
            "Search the invoices for the client 'harbour', in lower case, and check that Harbour Coffee's five invoices are found.",
            [
                Goto("Open the invoices", "/invoices"),
                Type("Type harbour", "search-client", "harbour"),
                Click("Search", "do-search"),
                Assert("Five invoices are found", "invoice-count", "Showing 5 of 5 invoices"),
            ],
            Autestiq.InvoiceDesk.Defects.ClientSearchCaseSensitive),
    ];

    /// <summary>The steps from the services page to a booking's details form.</summary>
    private static List<PlannedAction> Reach(string service, string day, string time) =>
    [
        Goto("Open the services", "/services"),
        Click($"Book the {service}", $"book-{service}"),
        Click($"Choose {day} November", $"date-2026-11-{day}"),
        Click($"Choose {time}", $"time-{time[..2]}"),
    ];

    /// <summary>The steps of booking one appointment, ending on the booking's own page.</summary>
    private static List<PlannedAction> Book(string service, string day, string time, string name, string email, string? voucher)
    {
        var steps = Reach(service, day, time);
        steps.Add(Type("Type the name", "full-name", name));
        steps.Add(Type("Type the email address", "email", email));

        if (voucher is not null)
        {
            steps.Add(Type("Type the voucher code", "voucher", voucher));
        }

        steps.Add(Click("Confirm the booking", "confirm-booking"));
        return steps;
    }
}
