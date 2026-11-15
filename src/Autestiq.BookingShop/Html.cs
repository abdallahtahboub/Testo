using System.Net;

namespace Autestiq.BookingShop;

/// <summary>Wraps a page's body in the clinic's layout. Everything user-supplied is encoded here or at its use.</summary>
internal static class Html
{
    public static string Page(string title, string body) => $"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>{Encode(title)}</title></head>
        <body>
        <header>
          <a href="/">Riverside Physiotherapy (demo)</a>
          <nav>
            <a data-testid="nav-services" href="/services">Services</a>
            <a data-testid="nav-find" href="/find">Find a booking</a>
            <a data-testid="nav-hours" href="/hours">Opening hours</a>
            <a data-testid="nav-contact" href="/contact">Contact</a>
          </nav>
        </header>
        <main>
        {body}
        </main>
        </body>
        </html>
        """;

    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
