using System.Globalization;
using Microsoft.AspNetCore.Routing;

namespace Autestiq.BookingShop;

/// <summary>Every page and form of the clinic, and the endpoints the benchmark uses to set it up.</summary>
public static class BookingShopRoutes
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", () => Page("Riverside Physiotherapy (demo)", """
            <h1>Riverside Physiotherapy</h1>
            <p>Physiotherapy in the city centre. This is a demonstration site, not a real clinic.</p>
            <p><a data-testid="home-book" href="/services">Book an appointment</a></p>
            """));

        endpoints.MapGet("/services", () => Page("Services", ServicesBody()));

        endpoints.MapGet("/book/{service}", (string service) =>
        {
            if (Clinic.Find(service) is not { } found)
            {
                return NotFoundPage();
            }

            var days = string.Concat(Clinic.OpenDays.Select(day =>
                $"<li><a data-testid=\"date-{day:yyyy-MM-dd}\" href=\"/book/{found.Id}/time?date={day:yyyy-MM-dd}\">{Html.Encode(Clinic.DayText(day))}</a></li>"));

            return Page($"Choose a date: {found.Name}", $"""
                <h1>Choose a date</h1>
                <p>For <span data-testid="chosen-service">{Html.Encode(found.Name)}</span>.</p>
                <ul>{days}</ul>
                """);
        });

        endpoints.MapGet("/book/{service}/time", (string service, string? date, string? error) =>
        {
            if (Clinic.Find(service) is not { } found || !DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day))
            {
                return NotFoundPage();
            }

            var slots = string.Concat(Clinic.Times.Select(time =>
            {
                var hour = time.Hour.ToString("00", CultureInfo.InvariantCulture);

                // A booked slot is shown as booked. The double-booking defect offers it again.
                return Clinic.IsBooked(day, time) && !Defects.On(Defects.DoubleBooking)
                    ? $"<li><span data-testid=\"booked-{hour}\">{Clinic.TimeText(time)} (booked)</span></li>"
                    : $"<li><a data-testid=\"time-{hour}\" href=\"/book/{found.Id}/details?date={day:yyyy-MM-dd}&amp;time={Clinic.TimeText(time)}\">{Clinic.TimeText(time)}</a></li>";
            }));

            var notice = error == "slot"
                ? "<p data-testid=\"error-slot\">That time has just been booked. Please choose another.</p>"
                : "";

            return Page($"Choose a time: {found.Name}", $"""
                <h1>Choose a time</h1>
                <p>{Html.Encode(Clinic.DayText(day))}</p>
                {notice}
                <ul>{slots}</ul>
                """);
        });

        endpoints.MapGet("/book/{service}/details", (string service, string? date, string? time, string? error) =>
        {
            if (Clinic.Find(service) is not { } found
                || !DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)
                || !TimeOnly.TryParseExact(time, "HH:mm", out var start))
            {
                return NotFoundPage();
            }

            var errors = error switch
            {
                "name" => "<p data-testid=\"error-name\">Please enter your name.</p>",
                "email" => "<p data-testid=\"error-email\">Please enter a valid email address.</p>",
                _ => "",
            };

            return Page($"Your details: {found.Name}", $"""
                <h1>Your details</h1>
                <p data-testid="detail-service">{Html.Encode(found.Name)}</p>
                <p data-testid="detail-when">{Html.Encode(Clinic.DayText(day))} at {Clinic.TimeText(start)}</p>
                <p data-testid="detail-price">Price before any voucher: {Clinic.Money(found.Price)}</p>
                {errors}
                <form method="post" action="/book">
                  <input type="hidden" name="service" value="{found.Id}" />
                  <input type="hidden" name="date" value="{day:yyyy-MM-dd}" />
                  <input type="hidden" name="time" value="{Clinic.TimeText(start)}" />
                  <label>Full name <input type="text" name="name" data-testid="full-name" /></label>
                  <label>Email <input type="text" name="email" data-testid="email" /></label>
                  <label>Phone (optional) <input type="text" name="phone" data-testid="phone" /></label>
                  <label>Notes (optional) <textarea name="notes" data-testid="notes"></textarea></label>
                  <label>Voucher code (optional) <input type="text" name="voucher" data-testid="voucher" /></label>
                  <button type="submit" data-testid="confirm-booking">Confirm booking</button>
                </form>
                """);
        });

        endpoints.MapPost("/book", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var serviceId = form["service"].ToString();
            var dateText = form["date"].ToString();
            var timeText = form["time"].ToString();
            var name = form["name"].ToString().Trim();
            var email = form["email"].ToString().Trim();

            if (Clinic.Find(serviceId) is not { } service
                || !DateOnly.TryParseExact(dateText, "yyyy-MM-dd", out var day)
                || !TimeOnly.TryParseExact(timeText, "HH:mm", out var start))
            {
                return Results.Redirect("/services");
            }

            var details = $"/book/{service.Id}/details?date={day:yyyy-MM-dd}&time={Clinic.TimeText(start)}";

            if (name.Length == 0)
            {
                return Results.Redirect($"{details}&error=name");
            }

            if (!Defects.On(Defects.EmailUnchecked) && !email.Contains('@', StringComparison.Ordinal))
            {
                return Results.Redirect($"{details}&error=email");
            }

            if (!Defects.On(Defects.DoubleBooking) && Clinic.IsBooked(day, start))
            {
                return Results.Redirect($"/book/{service.Id}/time?date={day:yyyy-MM-dd}&error=slot");
            }

            var booking = Clinic.Add(
                service,
                day,
                start,
                name,
                email,
                form["notes"].ToString().Trim(),
                form["voucher"].ToString());

            return Results.Redirect($"/booking/{booking.Reference}");
        });

        endpoints.MapGet("/booking/{reference}", (string reference, string? cancelled) =>
        {
            if (Clinic.FindByReference(reference) is not { } booking)
            {
                return NotFoundPage();
            }

            // A cancellation that was not saved still says so on the page it returns to.
            var shownCancelled = booking.Cancelled || cancelled == "1";

            return Page($"Booking {booking.Reference}", BookingBody(booking, shownCancelled, showCancel: !shownCancelled));
        });

        endpoints.MapPost("/booking/{reference}/cancel", (string reference) =>
        {
            if (Clinic.FindByReference(reference) is not { } booking)
            {
                return NotFoundPage();
            }

            Clinic.Cancel(booking);
            return Results.Redirect($"/booking/{booking.Reference}?cancelled=1");
        });

        endpoints.MapGet("/find", (string? reference, string? email) =>
        {
            if (reference is null && email is null)
            {
                return Page("Find a booking", FindForm(null));
            }

            var booking = Clinic.FindByReference(reference);
            var matches = booking is not null
                && string.Equals(booking.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase);

            if (booking is null || !matches)
            {
                return Page("Find a booking", FindForm("<p data-testid=\"find-error\">No booking matches those details.</p>"));
            }

            return Page("Find a booking", FindForm(null) + BookingBody(booking, booking.Cancelled, showCancel: false));
        });

        endpoints.MapGet("/hours", () => Page("Opening hours", """
            <h1>Opening hours</h1>
            <table>
              <tr><th>Monday to Friday</th><td data-testid="hours-weekdays">08:00 to 18:00</td></tr>
              <tr><th>Saturday</th><td data-testid="hours-saturday">09:00 to 13:00</td></tr>
              <tr><th>Sunday</th><td data-testid="hours-sunday">Closed</td></tr>
            </table>
            """));

        endpoints.MapGet("/contact", () => Page("Contact", """
            <h1>Contact</h1>
            <p>Telephone <span data-testid="contact-phone">0161 555 0148</span></p>
            <p>Email <span data-testid="contact-email">hello@riverside.example</span></p>
            """));

        endpoints.MapGet("/sitemap.xml", () => Results.Content("""
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>http://127.0.0.1/</loc></url>
              <url><loc>http://127.0.0.1/services</loc></url>
              <url><loc>http://127.0.0.1/find</loc></url>
              <url><loc>http://127.0.0.1/hours</loc></url>
              <url><loc>http://127.0.0.1/contact</loc></url>
            </urlset>
            """, "application/xml"));

        endpoints.MapGet("/health", () => Results.Text("ok"));

        // The benchmark's controls. A clinic that has been set up by a test is started afresh, so the
        // diary is cleared before each run and no run can take a slot from the next.
        endpoints.MapPost("/_reset", () =>
        {
            Clinic.Reset();
            return Results.Ok();
        });

        endpoints.MapGet("/_defects", () => Results.Json(new { on = Defects.Current, known = Defects.All }));

        endpoints.MapPost("/_defects", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var wanted = form["defects"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return Defects.Set(wanted)
                ? Results.Json(new { on = Defects.Current })
                : Results.BadRequest(new { error = "unknown defect", known = Defects.All });
        });
    }

    private static string ServicesBody()
    {
        var items = string.Concat(Clinic.Services.Select(service => $"""
            <article>
              <h2 data-testid="name-{service.Id}">{Html.Encode(service.Name)}</h2>
              <p data-testid="price-{service.Id}">{Clinic.Money(service.Price)}</p>
              <p>{Html.Encode(service.Description)}</p>
              <a data-testid="book-{service.Id}" href="/book/{service.Id}">Book this</a>
            </article>
            """));

        return $"<h1>Services</h1>{items}";
    }

    private static string FindForm(string? result) => $"""
        <h1>Find a booking</h1>
        <form method="get" action="/find">
          <label>Booking reference <input type="text" name="reference" data-testid="ref-input" /></label>
          <label>Email <input type="text" name="email" data-testid="email-input" /></label>
          <button type="submit" data-testid="find-submit">Find my booking</button>
        </form>
        {result}
        """;

    private static string BookingBody(Booking booking, bool shownCancelled, bool showCancel)
    {
        // With the time-shifted defect the confirmation gives the wrong hour, and says so with confidence.
        var shownTime = Defects.On(Defects.TimeShifted) ? booking.Time.AddHours(-1) : booking.Time;
        var status = shownCancelled ? "Cancelled" : "Confirmed";

        var cancel = showCancel
            ? $"<form method=\"post\" action=\"/booking/{booking.Reference}/cancel\"><button type=\"submit\" data-testid=\"cancel-booking\">Cancel this booking</button></form>"
            : "";

        return $"""
            <h1>Your booking</h1>
            <p>Reference <span data-testid="booking-ref">{Html.Encode(booking.Reference)}</span></p>
            <p>Treatment <span data-testid="booking-service">{Html.Encode(booking.Service.Name)}</span></p>
            <p>When <span data-testid="booking-when">{Html.Encode(Clinic.DayText(booking.Date))} at {Clinic.TimeText(shownTime)}</span></p>
            <p>Total <span data-testid="booking-total">{Clinic.Money(booking.Total)}</span></p>
            <p>Status <span data-testid="booking-status">{status}</span></p>
            {cancel}
            """;
    }

    private static IResult Page(string title, string body) =>
        Results.Content(Html.Page(title, body), "text/html; charset=utf-8");

    private static IResult NotFoundPage() =>
        Results.Content(Html.Page("Not found", "<h1>Not found</h1><p data-testid=\"not-found\">Nothing here matches that address.</p>"), "text/html; charset=utf-8", statusCode: 404);
}
