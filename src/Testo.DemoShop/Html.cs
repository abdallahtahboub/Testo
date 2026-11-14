namespace Testo.DemoShop;

/// <summary>The shop's markup. Plain, server-rendered HTML, because the point is to be ordinary.</summary>
public static class Html
{
    /// <summary>A whole page, as a response.</summary>
    public static IResult Page(string title, string body) =>
        Results.Content(
            $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8" /><title>{title} — Demo shop</title></head>
            <body>{body}</body>
            </html>
            """,
            "text/html; charset=utf-8");

    /// <summary>The account link, which says whether anybody is signed in.</summary>
    public static string Account(HttpContext context) =>
        Shop.SignedIn(context)
            ? "<p><a href=\"/account\" data-testid=\"to-account\">Your account</a></p>"
            : "<p><a href=\"/sign-in\" data-testid=\"to-sign-in\">Sign in</a></p>";
}
