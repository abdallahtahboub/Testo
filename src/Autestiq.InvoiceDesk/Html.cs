using System.Net;

namespace Autestiq.InvoiceDesk;

/// <summary>Wraps a page's body in the desk's layout. Everything a person typed is encoded at its use.</summary>
internal static class Html
{
    public static string Page(string title, string body) => $"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>{Encode(title)} — Ledgerly (demo)</title></head>
        <body>
        <header>
          <a href="/">Ledgerly (demo)</a>
          <nav>
            <a data-testid="nav-invoices" href="/invoices">Invoices</a>
            <a data-testid="nav-new-invoice" href="/invoices/new">New invoice</a>
            <a data-testid="nav-clients" href="/clients">Clients</a>
            <a data-testid="nav-reports" href="/reports">Reports</a>
          </nav>
        </header>
        <main>
        {body}
        </main>
        </body>
        </html>
        """;

    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");

    public static string Query(string? text) => WebUtility.UrlEncode(text ?? "");
}
