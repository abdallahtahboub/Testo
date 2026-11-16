using System.Globalization;
using Microsoft.AspNetCore.Routing;

namespace Autestiq.InvoiceDesk;

/// <summary>Every page and form of the invoice desk, and the endpoints the benchmark uses to set it up.</summary>
public static class InvoiceDeskRoutes
{
    private static readonly string[] Statuses = ["all", "draft", "sent", "paid", "overdue"];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", () => Page("Home", $"""
            <h1>Ledgerly</h1>
            <p>Invoices for a small business. This is a demonstration site, not a real service.</p>
            <p><a data-testid="home-overdue" href="/invoices?status=overdue">{Ledger.OverdueCount} overdue invoices</a></p>
            """));

        endpoints.MapGet("/invoices", (string? status, string? client, string? sort, int? page) =>
            Page("Invoices", ListBody(status, client, sort, page ?? 1)));

        endpoints.MapGet("/invoices/new", (string? error) => Page("New invoice", NewInvoiceBody(error)));

        endpoints.MapPost("/invoices", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();

            if (Ledger.FindClient(form["client"].ToString()) is not { } client)
            {
                return Results.Redirect("/invoices/new?error=client");
            }

            var description = form["description"].ToString().Trim();

            if (description.Length == 0
                || !int.TryParse(form["quantity"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
                || quantity < 1
                || !decimal.TryParse(form["price"].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                || price <= 0)
            {
                return Results.Redirect("/invoices/new?error=line");
            }

            var invoice = Ledger.Create(client, description, quantity, price);
            return Results.Redirect($"/invoices/{invoice.Number}");
        });

        endpoints.MapGet("/invoices/{number}", (string number, string? reminded) =>
            Ledger.Find(number) is { } invoice
                ? Page($"Invoice {invoice.Number}", InvoiceBody(invoice, reminded == "1"))
                : NotFoundPage());

        endpoints.MapPost("/invoices/{number}/paid", (string number) =>
        {
            if (Ledger.Find(number) is not { } invoice)
            {
                return NotFoundPage();
            }

            Ledger.MarkPaid(invoice);
            return Results.Redirect($"/invoices/{invoice.Number}");
        });

        endpoints.MapPost("/invoices/{number}/reminder", (string number) =>
            Ledger.Find(number) is { } invoice && invoice.IsOpen
                ? Results.Redirect($"/invoices/{invoice.Number}?reminded=1")
                : NotFoundPage());

        endpoints.MapGet("/clients", (string? added, string? error) => Page("Clients", ClientsBody(added, error)));

        endpoints.MapPost("/clients", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var name = form["name"].ToString().Trim();
            var email = form["email"].ToString().Trim();

            if (name.Length == 0)
            {
                return Results.Redirect("/clients?error=name");
            }

            if (Ledger.FindClient(name) is not null)
            {
                return Results.Redirect("/clients?error=duplicate");
            }

            if (!Defects.On(Defects.ClientEmailUnchecked) && !LooksLikeAnEmail(email))
            {
                return Results.Redirect("/clients?error=email");
            }

            Ledger.AddClient(new Client(name, email));
            return Results.Redirect($"/clients?added={Html.Query(name)}");
        });

        endpoints.MapGet("/reports", () => Page("Reports", $"""
            <h1>Reports</h1>
            <table>
              <tr><th>Outstanding (sent and overdue)</th><td data-testid="outstanding-total">{Ledger.Money(Ledger.Outstanding)}</td></tr>
              <tr><th>Paid this month</th><td data-testid="paid-this-month">{Ledger.Money(Ledger.PaidThisMonth)}</td></tr>
              <tr><th>Overdue invoices</th><td data-testid="overdue-count">{Ledger.OverdueCount}</td></tr>
            </table>
            """));

        endpoints.MapGet("/sitemap.xml", () => Results.Content("""
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>http://127.0.0.1/</loc></url>
              <url><loc>http://127.0.0.1/invoices</loc></url>
              <url><loc>http://127.0.0.1/invoices/new</loc></url>
              <url><loc>http://127.0.0.1/clients</loc></url>
              <url><loc>http://127.0.0.1/reports</loc></url>
            </urlset>
            """, "application/xml"));

        endpoints.MapGet("/health", () => Results.Text("ok"));

        // The benchmark's controls: the ledger is put back to its seed before each run.
        endpoints.MapPost("/_reset", () =>
        {
            Ledger.Reset();
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

    private static string ListBody(string? status, string? client, string? sort, int page)
    {
        var chosen = Statuses.Contains(status ?? "all", StringComparer.OrdinalIgnoreCase) ? (status ?? "all").ToLowerInvariant() : "all";
        var (invoices, matching, pages) = Ledger.List(chosen, client, sort, page);

        string Link(string? toStatus = null, string? toSort = null, int toPage = 1)
        {
            var parts = new List<string>();

            if ((toStatus ?? chosen) is { } s && s != "all")
            {
                parts.Add("status=" + s);
            }

            if (client is { Length: > 0 })
            {
                parts.Add("client=" + Html.Query(client));
            }

            if ((toSort ?? sort) is { Length: > 0 } o)
            {
                parts.Add("sort=" + o);
            }

            if (toPage > 1)
            {
                parts.Add("page=" + toPage.ToString(CultureInfo.InvariantCulture));
            }

            return "/invoices" + (parts.Count > 0 ? "?" + string.Join("&amp;", parts) : "");
        }

        var filters = string.Concat(Statuses.Select(s =>
            $"<a data-testid=\"filter-{s}\" href=\"{(s == "all" ? "/invoices" : "/invoices?status=" + s)}\">{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s)}</a> "));

        var rows = string.Concat(invoices.Select(invoice => $"""
            <tr data-testid="row-{invoice.Number}">
              <td><a data-testid="open-{invoice.Number}" href="/invoices/{invoice.Number}">{invoice.Number}</a></td>
              <td data-testid="client-{invoice.Number}">{Html.Encode(invoice.Client)}</td>
              <td>{Ledger.DateText(invoice.Issued)}</td>
              <td data-testid="total-{invoice.Number}">{Ledger.Money(invoice.Total)}</td>
              <td data-testid="status-{invoice.Number}">{invoice.Status}</td>
            </tr>
            """));

        var current = Math.Clamp(page, 1, pages);
        var paging = string.Concat(Enumerable.Range(1, pages).Select(p =>
            $"<a data-testid=\"page-{p}\" href=\"{Link(toPage: p)}\">{p}</a> "));

        if (current < pages)
        {
            paging += $"<a data-testid=\"next-page\" href=\"{Link(toPage: current + 1)}\">Next</a>";
        }

        var table = invoices.Count == 0
            ? "<p data-testid=\"no-invoices\">No invoices match.</p>"
            : $"<table><tr><th>Number</th><th>Client</th><th>Issued</th><th>Total</th><th>Status</th></tr>{rows}</table>";

        return $"""
            <h1>Invoices</h1>
            <p>Show: {filters}</p>
            <form method="get" action="/invoices" role="search">
              <label>Client <input type="search" name="client" data-testid="search-client" value="{Html.Encode(client)}" /></label>
              <button type="submit" data-testid="do-search">Search</button>
            </form>
            <p><a data-testid="sort-total" href="{Link(toSort: "total")}">Largest first</a></p>
            <p data-testid="invoice-count">Showing {invoices.Count} of {matching} invoices</p>
            {table}
            <p>Page: {paging}</p>
            """;
    }

    private static string NewInvoiceBody(string? error)
    {
        var message = error switch
        {
            "client" => "<p data-testid=\"error-client\">There is no client by that name.</p>",
            "line" => "<p data-testid=\"error-line\">Give the line a description, a whole quantity of at least one and a price above zero.</p>",
            _ => "",
        };

        return $"""
            <h1>New invoice</h1>
            {message}
            <form method="post" action="/invoices">
              <label>Client <input type="text" name="client" data-testid="client-name" /></label>
              <label>Description <input type="text" name="description" data-testid="description" /></label>
              <label>Quantity <input type="text" name="quantity" data-testid="quantity" /></label>
              <label>Unit price <input type="text" name="price" data-testid="unit-price" /></label>
              <p>VAT is added at 20%.</p>
              <button type="submit" data-testid="create-invoice">Create invoice</button>
            </form>
            """;
    }

    private static string InvoiceBody(Invoice invoice, bool reminded)
    {
        var email = Ledger.FindClient(invoice.Client)?.Email ?? "";

        var actions = invoice.IsOpen
            ? $"""
              <form method="post" action="/invoices/{invoice.Number}/paid"><button type="submit" data-testid="mark-paid">Mark as paid</button></form>
              <form method="post" action="/invoices/{invoice.Number}/reminder"><button type="submit" data-testid="send-reminder">Send reminder</button></form>
              """
            : "";

        var notice = reminded
            ? $"<p data-testid=\"reminder-sent\">Reminder sent to {Html.Encode(email)}.</p>"
            : "";

        var paid = invoice.PaidOn is { } on
            ? $"<p>Paid on <span data-testid=\"paid-on\">{Ledger.DateText(on)}</span></p>"
            : "";

        return $"""
            <h1>Invoice <span data-testid="invoice-number">{invoice.Number}</span></h1>
            {notice}
            <p>Client <span data-testid="invoice-client">{Html.Encode(invoice.Client)}</span></p>
            <p>Issued <span data-testid="invoice-issued">{Ledger.DateText(invoice.Issued)}</span></p>
            <p>Status <span data-testid="invoice-status">{invoice.Status}</span></p>
            {paid}
            <table>
              <tr><th>Description</th><th>Quantity</th><th>Unit price</th></tr>
              <tr>
                <td data-testid="line-description">{Html.Encode(invoice.Description)}</td>
                <td data-testid="line-quantity">{invoice.Quantity}</td>
                <td data-testid="line-unit-price">{Ledger.Money(invoice.UnitPrice)}</td>
              </tr>
            </table>
            <p>Subtotal <span data-testid="subtotal">{Ledger.Money(invoice.Subtotal)}</span></p>
            <p>VAT at 20% <span data-testid="vat">{Ledger.Money(invoice.Vat)}</span></p>
            <p>Total <span data-testid="total">{Ledger.Money(invoice.Total)}</span></p>
            {actions}
            """;
    }

    private static string ClientsBody(string? added, string? error)
    {
        var rows = string.Concat(Ledger.Clients.Select(client => $"""
            <tr>
              <td data-testid="client-name-{client.Slug}">{Html.Encode(client.Name)}</td>
              <td data-testid="client-email-{client.Slug}">{Html.Encode(client.Email)}</td>
            </tr>
            """));

        var message = error switch
        {
            "name" => "<p data-testid=\"error-client-name\">Give the client a name.</p>",
            "duplicate" => "<p data-testid=\"error-client-name\">There is already a client called that.</p>",
            "email" => "<p data-testid=\"error-client-email\">That is not an email address.</p>",
            _ => added is { Length: > 0 } ? $"<p data-testid=\"client-added\">Added {Html.Encode(added)}.</p>" : "",
        };

        return $"""
            <h1>Clients</h1>
            {message}
            <p data-testid="client-count">{Ledger.Clients.Count} clients</p>
            <table><tr><th>Name</th><th>Email</th></tr>{rows}</table>
            <h2>Add a client</h2>
            <form method="post" action="/clients">
              <label>Name <input type="text" name="name" data-testid="new-client-name" /></label>
              <label>Email <input type="text" name="email" data-testid="new-client-email" /></label>
              <button type="submit" data-testid="add-client">Add client</button>
            </form>
            """;
    }

    private static bool LooksLikeAnEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && email.IndexOf('.', at) > at + 1 && !email.EndsWith('.');
    }

    private static IResult Page(string title, string body) =>
        Results.Content(Html.Page(title, body), "text/html; charset=utf-8");

    private static IResult NotFoundPage() =>
        Results.Content(Html.Page("Not found", "<h1>Not found</h1><p data-testid=\"not-found\">Nothing here matches that address.</p>"), "text/html; charset=utf-8", statusCode: 404);
}
