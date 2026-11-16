using Autestiq.BookingShop;
using Autestiq.Contracts.Planning;
using static Autestiq.Benchmark.BenchmarkScenarios;

namespace Autestiq.Benchmark;

/// <summary>
/// Twenty scenarios for the second application, the physiotherapy clinic, written before any planner
/// has been measured on it. The freeze rule of <see cref="HeldOutScenarios"/> applies: a scenario, its
/// baseline or its defect may change only if its baseline fails a sound clinic or passes a broken one,
/// and only before the first planner run.
/// </summary>
/// <remarks>
/// <para>
/// The clinic is a different shape from the shop: a diary of dates and times, a form with validation,
/// references to look up, and a cancellation. The planner has never seen it. Its wording of the tasks is
/// a tester's, and nothing in a scenario names an element beyond what a tester could see on the page.
/// </para>
/// <para>
/// Fourteen only run against a sound clinic. The six with a defect cover each of the clinic's six defects.
/// The clinic starts its diary afresh before every run, so the first booking of any run is BK-1001.
/// </para>
/// </remarks>
public static class BookingScenarios
{
    /// <summary>Every booking-clinic scenario.</summary>
    public static IReadOnlyList<BenchmarkScenario> All { get; } =
    [
        // --- Sound clinic only: passing is correct. ---

        new(
            "services-list-the-four-treatments",
            "Open the services and check the four treatments are listed by name.",
            [
                Goto("Open the services", "/services"),
                Assert("The initial assessment is listed", "name-initial", "Initial assessment"),
                Assert("The follow-up is listed", "name-follow", "Follow-up appointment"),
                Assert("The sports massage is listed", "name-massage", "Sports massage"),
                Assert("The back pain course is listed", "name-package", "Back pain course (6 sessions)"),
            ]),

        new(
            "services-show-their-prices",
            "Check the prices on the services page: the initial assessment is £60.00, the follow-up £45.00, the sports massage £50.00 and the back pain course £270.00.",
            [
                Goto("Open the services", "/services"),
                Assert("The initial assessment costs £60.00", "price-initial", "£60.00"),
                Assert("The follow-up costs £45.00", "price-follow", "£45.00"),
                Assert("The sports massage costs £50.00", "price-massage", "£50.00"),
                Assert("The back pain course costs £270.00", "price-package", "£270.00"),
            ]),

        new(
            "contact-page-gives-the-phone-number",
            "Open the contact page and check the telephone number is 0161 555 0148.",
            [
                Goto("Open the contact page", "/contact"),
                Assert("The telephone number is shown", "contact-phone", "0161 555 0148"),
            ]),

        new(
            "saturday-hours-are-morning-only",
            "Open the opening hours and check that on Saturday the clinic is open from 09:00 to 13:00.",
            [
                Goto("Open the opening hours", "/hours"),
                Assert("Saturday is 09:00 to 13:00", "hours-saturday", "09:00 to 13:00"),
            ]),

        new(
            "time-page-offers-the-morning-slots",
            "Book an initial assessment for Monday 16 November and check the 9:00 and 10:00 slots are offered.",
            [
                Goto("Open the services", "/services"),
                Click("Book the initial assessment", "book-initial"),
                Click("Choose Monday 16 November", "date-2026-11-16"),
                AssertVisible("The 9:00 slot is offered", "time-09"),
                AssertVisible("The 10:00 slot is offered", "time-10"),
            ]),

        new(
            "email-is-required",
            "Book an initial assessment for Monday 16 November at 10:00, give a name but no email address, and check the clinic asks for an email address.",
            [
                Goto("Open the services", "/services"),
                Click("Book the initial assessment", "book-initial"),
                Click("Choose Monday 16 November", "date-2026-11-16"),
                Click("Choose 10:00", "time-10"),
                Type("Type a name", "full-name", "Ada Lovelace"),
                Click("Confirm the booking", "confirm-booking"),
                AssertVisible("It asks for an email address", "error-email"),
            ]),

        new(
            "name-is-required",
            "Book an initial assessment for Monday 16 November at 10:00, give an email address but no name, and check the clinic asks for a name.",
            [
                Goto("Open the services", "/services"),
                Click("Book the initial assessment", "book-initial"),
                Click("Choose Monday 16 November", "date-2026-11-16"),
                Click("Choose 10:00", "time-10"),
                Type("Type an email address", "email", "ada@example.test"),
                Click("Confirm the booking", "confirm-booking"),
                AssertVisible("It asks for a name", "error-name"),
            ]),

        new(
            "confirmation-shows-the-booking",
            "Book an initial assessment for Monday 16 November at 10:00 and check the confirmation says it is confirmed and gives the right day and time.",
            Book("initial", "16", "10:00", "Ada Lovelace", "ada@example.test", null,
                Assert("The day and time are right", "booking-when", "Monday 16 November 2026 at 10:00"),
                Assert("The booking is confirmed", "booking-status", "Confirmed")),
            Defect: null),

        new(
            "welcome-voucher-takes-off-a-fifth",
            "Book a sports massage for Monday 16 November at 11:00 with the voucher WELCOME20 and check the total is £40.00.",
            Book("massage", "16", "11:00", "Ada Lovelace", "ada@example.test", "WELCOME20",
                Assert("The total is £40.00", "booking-total", "£40.00")),
            Defect: null),

        new(
            "unknown-voucher-changes-nothing",
            "Book a sports massage for Monday 16 November at 11:00 with the voucher FREE100 and check the total is still £50.00.",
            Book("massage", "16", "11:00", "Ada Lovelace", "ada@example.test", "FREE100",
                Assert("The total is still £50.00", "booking-total", "£50.00")),
            Defect: null),

        new(
            "find-a-booking-by-reference",
            "Book an initial assessment for Monday 16 November at 11:00. Then use the find page with the booking reference and the email address, and check the booking is found and confirmed.",
            [
                .. Book("initial", "16", "11:00", "Ada Lovelace", "ada@example.test", null),
                Goto("Open the find page", "/find"),
                Type("Type the reference BK-1001", "ref-input", "BK-1001"),
                Type("Type the email address", "email-input", "ada@example.test"),
                Click("Find the booking", "find-submit"),
                Assert("The booking is confirmed", "booking-status", "Confirmed"),
            ]),

        new(
            "find-refuses-the-wrong-email",
            "Book an initial assessment for Monday 16 November at 11:00. Then look it up by reference with a different email address, and check the clinic says no booking matches.",
            [
                .. Book("initial", "16", "11:00", "Ada Lovelace", "ada@example.test", null),
                Goto("Open the find page", "/find"),
                Type("Type the reference BK-1001", "ref-input", "BK-1001"),
                Type("Type a different email address", "email-input", "someone@example.test"),
                Click("Find the booking", "find-submit"),
                AssertVisible("It says no booking matches", "find-error"),
            ]),

        new(
            "cancelled-booking-stays-cancelled",
            "Book an initial assessment for Monday 16 November at 11:00 and cancel it. Then look the booking up by reference and check it shows as cancelled.",
            [
                .. Book("initial", "16", "11:00", "Ada Lovelace", "ada@example.test", null),
                Click("Cancel the booking", "cancel-booking"),
                Goto("Open the find page", "/find"),
                Type("Type the reference BK-1001", "ref-input", "BK-1001"),
                Type("Type the email address", "email-input", "ada@example.test"),
                Click("Find the booking", "find-submit"),
                Assert("The booking shows as cancelled", "booking-status", "Cancelled"),
            ]),

        new(
            "hours-page-says-sunday-is-closed",
            "Open the opening hours and check the clinic is closed on Sunday.",
            [
                Goto("Open the opening hours", "/hours"),
                Assert("Sunday is closed", "hours-sunday", "Closed"),
            ]),

        // --- Each of the clinic's six defects, worded differently from one another. Passing is a false positive. ---

        new(
            "booked-slot-stays-taken",
            "Book an initial assessment for Monday 16 November at 9:00. Then look at the same day for a follow-up and check the 9:00 slot shows as booked.",
            [
                .. Book("initial", "16", "09:00", "Ada Lovelace", "ada@example.test", null),
                Goto("Open the services", "/services"),
                Click("Book the follow-up", "book-follow"),
                Click("Choose Monday 16 November", "date-2026-11-16"),
                AssertVisible("The 9:00 slot shows as booked", "booked-09"),
            ],
            Defects.DoubleBooking),

        new(
            "confirmation-gives-the-booked-hour",
            "Book an initial assessment for Monday 16 November at 10:00 and check the confirmation gives the same time.",
            Book("initial", "16", "10:00", "Ada Lovelace", "ada@example.test", null,
                Assert("The confirmation gives 10:00", "booking-when", "Monday 16 November 2026 at 10:00")),
            Defects.TimeShifted),

        new(
            "cancelling-one-leaves-the-other",
            "Book two initial assessments under the same email address, for Monday 16 November at 9:00 and at 10:00. Cancel the one at 10:00, then look it up and check it shows as cancelled.",
            [
                .. Book("initial", "16", "09:00", "Ada Lovelace", "ada@example.test", null),
                .. Book("initial", "16", "10:00", "Ada Lovelace", "ada@example.test", null),
                Click("Cancel the 10:00 booking", "cancel-booking"),
                Goto("Open the find page", "/find"),
                Type("Type the reference BK-1002", "ref-input", "BK-1002"),
                Type("Type the email address", "email-input", "ada@example.test"),
                Click("Find the booking", "find-submit"),
                Assert("The 10:00 booking shows as cancelled", "booking-status", "Cancelled"),
            ],
            Defects.CancelWrong),

        new(
            "package-voucher-applies-to-the-course",
            "Book the six-session back pain course for Monday 16 November at 14:00 with the voucher WELCOME20 and check the total is £216.00.",
            Book("package", "16", "14:00", "Ada Lovelace", "ada@example.test", "WELCOME20",
                Assert("The total is £216.00", "booking-total", "£216.00")),
            Defects.PackageIgnoresVoucher),

        new(
            "address-must-look-like-an-email",
            "Book an initial assessment for Monday 16 November at 10:00 with the email address 'not-an-email' and check the clinic refuses it.",
            [
                Goto("Open the services", "/services"),
                Click("Book the initial assessment", "book-initial"),
                Click("Choose Monday 16 November", "date-2026-11-16"),
                Click("Choose 10:00", "time-10"),
                Type("Type a name", "full-name", "Ada Lovelace"),
                Type("Type an email address that is not one", "email", "not-an-email"),
                Click("Confirm the booking", "confirm-booking"),
                AssertVisible("It refuses the email address", "error-email"),
            ],
            Defects.EmailUnchecked),

        new(
            "cancellation-is-kept",
            "Book an initial assessment for Monday 16 November at 11:00 and cancel it. Then look the booking up by reference and email, and check it still shows as cancelled.",
            [
                .. Book("initial", "16", "11:00", "Ada Lovelace", "ada@example.test", null),
                Click("Cancel the booking", "cancel-booking"),
                Goto("Open the find page", "/find"),
                Type("Type the reference BK-1001", "ref-input", "BK-1001"),
                Type("Type the email address", "email-input", "ada@example.test"),
                Click("Find the booking", "find-submit"),
                Assert("The booking still shows as cancelled", "booking-status", "Cancelled"),
            ],
            Defects.CancelNotSaved),
    ];

    /// <summary>
    /// The steps of booking one appointment: the service, a day, a time, the details, and the
    /// confirmation. Ends where the booking's own page is, so that a check can follow it.
    /// </summary>
    private static List<PlannedAction> Book(
        string service,
        string day,
        string time,
        string name,
        string email,
        string? voucher,
        params PlannedAction[] then)
    {
        var steps = new List<PlannedAction>
        {
            Goto("Open the services", "/services"),
            Click($"Book the {ServiceName(service)}", $"book-{service}"),
            Click($"Choose Monday {day} November", $"date-2026-11-{day}"),
            Click($"Choose {time}", $"time-{time[..2]}"),
            Type("Type the name", "full-name", name),
            Type("Type the email address", "email", email),
        };

        if (voucher is not null)
        {
            steps.Add(Type("Type the voucher code", "voucher", voucher));
        }

        steps.Add(Click("Confirm the booking", "confirm-booking"));
        steps.AddRange(then);

        return steps;
    }

    private static string ServiceName(string service) => service switch
    {
        "initial" => "initial assessment",
        "follow" => "follow-up appointment",
        "massage" => "sports massage",
        "package" => "back pain course",
        _ => service,
    };
}
