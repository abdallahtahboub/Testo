using Autestiq.InvoiceDesk;
using static Autestiq.Benchmark.BenchmarkScenarios;

namespace Autestiq.Benchmark;

/// <summary>
/// Twenty scenarios for the third application, the invoice desk, written before any planner has been
/// measured on it, and after the explorer's rules were settled on the other two applications.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Freeze rule</strong> (the same as <see cref="HeldOutScenarios"/>): a scenario, its baseline or its
/// defect may change only if its baseline fails a sound desk or passes a broken one, and only before the
/// first planner run. Nor may the explorer or the planner be changed because of a result on this set and
/// the set then be called clean: after that it is a design set, like the other two.
/// </para>
/// <para>
/// The desk is a different shape again: a table of twenty-five invoices across three pages, filters,
/// a search, a sort, forms that check what they are given, and actions that change an invoice. Fourteen
/// scenarios run against a sound desk only; the six with a defect cover each of its six defects. The desk
/// is put back to its seed before every run.
/// </para>
/// </remarks>
public static class InvoiceScenarios
{
    /// <summary>Every invoice-desk scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        // --- Sound desk only: passing is correct. ---

        new(
            "first-page-lists-ten-invoices",
            "Open the invoices and check the first page shows ten of the twenty-five, from INV-1001 to INV-1010, and not INV-1011.",
            [
                Goto("Open the invoices", "/invoices"),
                Assert("It says ten of twenty-five are shown", "invoice-count", "Showing 10 of 25 invoices"),
                AssertVisible("INV-1001 is listed", "row-INV-1001"),
                AssertVisible("INV-1010 is listed", "row-INV-1010"),
                AssertHidden("INV-1011 is not on this page", "row-INV-1011"),
            ]),

        new(
            "third-page-holds-the-last-five",
            "Go to the third page of invoices and check it holds the last five, INV-1021 to INV-1025.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Go to page 3", "page-3"),
                Assert("It says five are shown", "invoice-count", "Showing 5 of 25 invoices"),
                AssertVisible("INV-1021 is listed", "row-INV-1021"),
                AssertVisible("INV-1025 is listed", "row-INV-1025"),
            ]),

        new(
            "overdue-filter-shows-the-three-overdue",
            "Show only overdue invoices and check there are three, including INV-1014, and that INV-1005, which has only been sent, is not among them.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Show only overdue invoices", "filter-overdue"),
                Assert("Three are shown", "invoice-count", "Showing 3 of 3 invoices"),
                AssertVisible("INV-1014 is listed", "row-INV-1014"),
                AssertHidden("INV-1005 is not listed", "row-INV-1005"),
            ]),

        new(
            "search-finds-a-clients-invoices",
            "Search the invoices for the client 'Lantern' and check that Lantern Books' four invoices are shown and Harbour Coffee's INV-1001 is not.",
            [
                Goto("Open the invoices", "/invoices"),
                Type("Search for Lantern", "search-client", "Lantern"),
                Click("Search", "do-search"),
                Assert("Four are shown", "invoice-count", "Showing 4 of 4 invoices"),
                AssertVisible("INV-1006 is listed", "row-INV-1006"),
                AssertHidden("INV-1001 is not listed", "row-INV-1001"),
            ]),

        new(
            "largest-first-puts-the-biggest-on-page-one",
            "Sort the invoices largest first and check that INV-1002, the largest at £1,080.00, is on the first page and INV-1009, the smallest, is not.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Sort largest first", "sort-total"),
                Assert("INV-1002 is listed at £1,080.00", "total-INV-1002", "£1,080.00"),
                AssertHidden("INV-1009 is not on the first page", "row-INV-1009"),
            ]),

        new(
            "invoice-shows-its-sums",
            "Open invoice INV-1003 and check its subtotal is £256.50, the VAT £51.30 and the total £307.80.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Open INV-1003", "open-INV-1003"),
                Assert("The subtotal is £256.50", "subtotal", "£256.50"),
                Assert("The VAT is £51.30", "vat", "£51.30"),
                Assert("The total is £307.80", "total", "£307.80"),
            ]),

        new(
            "new-invoice-for-a-client",
            "Create an invoice for Pine & Co for 2 of 'Gate repair' at £40.00 each, and check it is INV-1026, a draft, for £96.00 including VAT.",
            [
                Goto("Open the new invoice form", "/invoices/new"),
                Type("Type the client", "client-name", "Pine & Co"),
                Type("Type the description", "description", "Gate repair"),
                Type("Type the quantity", "quantity", "2"),
                Type("Type the unit price", "unit-price", "40.00"),
                Click("Create the invoice", "create-invoice"),
                Assert("It is INV-1026", "invoice-number", "INV-1026"),
                Assert("It is a draft", "invoice-status", "Draft"),
                Assert("The total is £96.00", "total", "£96.00"),
            ]),

        new(
            "new-invoice-needs-a-known-client",
            "Try to create an invoice for 'Nobody Ltd', a client the business does not have, and check the desk refuses it.",
            [
                Goto("Open the new invoice form", "/invoices/new"),
                Type("Type a client that does not exist", "client-name", "Nobody Ltd"),
                Type("Type the description", "description", "Consulting"),
                Type("Type the quantity", "quantity", "1"),
                Type("Type the unit price", "unit-price", "100.00"),
                Click("Try to create the invoice", "create-invoice"),
                AssertVisible("It says there is no such client", "error-client"),
            ]),

        new(
            "new-invoice-needs-a-quantity",
            "Try to create an invoice for Pine & Co with a quantity of 0 and check the desk refuses the line.",
            [
                Goto("Open the new invoice form", "/invoices/new"),
                Type("Type the client", "client-name", "Pine & Co"),
                Type("Type the description", "description", "Gate repair"),
                Type("Type a quantity of zero", "quantity", "0"),
                Type("Type the unit price", "unit-price", "40.00"),
                Click("Try to create the invoice", "create-invoice"),
                AssertVisible("It refuses the line", "error-line"),
            ]),

        new(
            "add-a-client",
            "Add a client called Willow Florist with the email hello@willow.example, and check the desk says it was added, lists the email and now has seven clients.",
            [
                Goto("Open the clients", "/clients"),
                Type("Type the name", "new-client-name", "Willow Florist"),
                Type("Type the email address", "new-client-email", "hello@willow.example"),
                Click("Add the client", "add-client"),
                AssertVisible("It says the client was added", "client-added"),
                Assert("The email is listed", "client-email-willow-florist", "hello@willow.example"),
                Assert("There are seven clients", "client-count", "7 clients"),
            ]),

        new(
            "a-second-client-with-the-same-name-is-refused",
            "Try to add another client called Harbour Coffee and check the desk refuses it and still has six clients.",
            [
                Goto("Open the clients", "/clients"),
                Type("Type a name that is taken", "new-client-name", "Harbour Coffee"),
                Type("Type an email address", "new-client-email", "second@harbour.example"),
                Click("Try to add the client", "add-client"),
                AssertVisible("It refuses the name", "error-client-name"),
                Assert("There are still six clients", "client-count", "6 clients"),
            ]),

        new(
            "report-shows-this-months-payments",
            "Open the reports and check that £882.00 was paid this month and three invoices are overdue.",
            [
                Goto("Open the reports", "/reports"),
                Assert("£882.00 was paid this month", "paid-this-month", "£882.00"),
                Assert("Three are overdue", "overdue-count", "3"),
            ]),

        new(
            "reminder-goes-to-the-clients-address",
            "Send a reminder for invoice INV-1008 and check the desk says it went to studio@northgate.example.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Open INV-1008", "open-INV-1008"),
                Click("Send a reminder", "send-reminder"),
                Assert("It went to the client's address", "reminder-sent", "Reminder sent to studio@northgate.example."),
            ]),

        new(
            "a-paid-invoice-offers-nothing-to-do",
            "Open invoice INV-1001, which is paid, and check it offers neither marking it as paid nor sending a reminder.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Open INV-1001", "open-INV-1001"),
                Assert("It is paid", "invoice-status", "Paid"),
                AssertHidden("It cannot be marked as paid again", "mark-paid"),
                AssertHidden("It offers no reminder", "send-reminder"),
            ]),

        // --- Each of the desk's six defects. Passing is a false positive. ---

        new(
            "vat-is-rounded-to-the-nearest-penny",
            "Open invoice INV-1009, a £12.34 repair, and check the VAT is £2.47 and the total £14.81.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Open INV-1009", "open-INV-1009"),
                Assert("The VAT is £2.47", "vat", "£2.47"),
                Assert("The total is £14.81", "total", "£14.81"),
            ],
            Defects.VatRoundedDown),

        new(
            "draft-filter-shows-only-drafts",
            "Show only draft invoices and check there are six, and that INV-1001, which is paid, is not among them.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Show only drafts", "filter-draft"),
                Assert("Six are shown", "invoice-count", "Showing 6 of 6 invoices"),
                AssertHidden("INV-1001 is not listed", "row-INV-1001"),
            ],
            Defects.FilterIgnored),

        new(
            "second-page-starts-at-the-eleventh",
            "Go to the second page of invoices and check it starts with INV-1011 and shows ten invoices.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Go to page 2", "page-2"),
                AssertVisible("INV-1011 is listed", "row-INV-1011"),
                Assert("Ten are shown", "invoice-count", "Showing 10 of 25 invoices"),
            ],
            Defects.PageSkipsOne),

        new(
            "marking-paid-marks-that-invoice",
            "Mark invoice INV-1005 as paid and check it now shows as paid, on 15 Nov 2026.",
            [
                Goto("Open the invoices", "/invoices"),
                Click("Open INV-1005", "open-INV-1005"),
                Click("Mark it as paid", "mark-paid"),
                Assert("It shows as paid", "invoice-status", "Paid"),
                Assert("It was paid today", "paid-on", "15 Nov 2026"),
            ],
            Defects.PaidMarksAnother),

        new(
            "outstanding-counts-only-unpaid-invoices",
            "Open the reports and check the outstanding total, of invoices sent or overdue, is £3,282.00.",
            [
                Goto("Open the reports", "/reports"),
                Assert("£3,282.00 is outstanding", "outstanding-total", "£3,282.00"),
            ],
            Defects.OutstandingIncludesPaid),

        new(
            "a-clients-email-must-be-an-address",
            "Try to add a client called Willow Florist with the email 'not-an-email' and check the desk refuses the address.",
            [
                Goto("Open the clients", "/clients"),
                Type("Type the name", "new-client-name", "Willow Florist"),
                Type("Type an address that is not one", "new-client-email", "not-an-email"),
                Click("Try to add the client", "add-client"),
                AssertVisible("It refuses the address", "error-client-email"),
            ],
            Defects.ClientEmailUnchecked),
    ];
}
