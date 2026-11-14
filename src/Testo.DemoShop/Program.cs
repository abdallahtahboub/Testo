using System.Globalization;
using Microsoft.AspNetCore.Http.Extensions;

// A deliberately small web shop for test runs to act on.
//
// It is not a demo of Testo. It is the thing Testo points at, and it exists so that the execution
// slice can be built and tested without aiming a browser at anybody else's website
// (docs/execution-threat-model.md §4.2).
//
// The checkout total is wrong on purpose. That is what the failing scenario finds, and it has to
// be a real defect in a real application for the failure to mean anything.

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();

const string BasketCookie = "demoshop.basket";

var catalogue = new (string Id, string Name, decimal Price)[]
{
    ("tea", "Loose leaf tea, 200g", 8.50m),
    ("pot", "Cast iron teapot", 42.00m),
    ("cup", "Porcelain cup", 11.25m),
};

app.MapGet("/", (HttpContext context) =>
{
    var rows = string.Join("", catalogue.Select(item => $"""
        <li>
          <span data-testid="name-{item.Id}">{item.Name}</span>
          <span>{item.Price.ToString("C", CultureInfo.GetCultureInfo("en-GB"))}</span>
          <form method="post" action="/basket/add">
            <input type="hidden" name="id" value="{item.Id}" />
            <button type="submit" data-testid="add-{item.Id}">Add to basket</button>
          </form>
        </li>
        """));

    return Html(Page("Catalogue", $"<h1>Catalogue</h1><ul class=\"catalogue\">{rows}</ul>"
        + $"<p><a href=\"/basket\" data-testid=\"to-basket\">Basket ({Count(context)})</a></p>"));
});

app.MapPost("/basket/add", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var id = form["id"].ToString();

    if (catalogue.Any(item => item.Id == id))
    {
        var basket = Read(context).Append(id).ToArray();
        context.Response.Cookies.Append(BasketCookie, string.Join(',', basket), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
    }

    return Results.Redirect("/basket");
});

app.MapGet("/basket", (HttpContext context) =>
{
    var basket = Read(context).ToArray();

    if (basket.Length == 0)
    {
        return Html(Page("Basket", "<h1>Basket</h1><p data-testid=\"empty\">Your basket is empty.</p>"));
    }

    var rows = string.Join("", basket.Select(id =>
    {
        var item = catalogue.First(entry => entry.Id == id);
        return $"<li data-testid=\"line-{item.Id}\">{item.Name} — {item.Price.ToString("C", CultureInfo.GetCultureInfo("en-GB"))}</li>";
    }));

    var total = basket.Sum(id => catalogue.First(entry => entry.Id == id).Price);

    return Html(Page("Basket", $"""
        <h1>Basket</h1>
        <ul data-testid="lines">{rows}</ul>
        <p>Total: <span data-testid="basket-total">{total.ToString("C", CultureInfo.GetCultureInfo("en-GB"))}</span></p>
        <p><a href="/checkout" data-testid="to-checkout">Checkout</a></p>
        """));
});

app.MapGet("/checkout", (HttpContext context) =>
{
    var basket = Read(context).ToArray();
    var total = basket.Sum(id => catalogue.First(entry => entry.Id == id).Price);

    // The defect, on purpose and in one place: the second and later items are left out of the
    // checkout total. The basket says one number and the checkout says another, which is the
    // kind of thing a QA platform exists to notice.
    var charged = basket.Length == 0 ? 0m : catalogue.First(entry => entry.Id == basket[0]).Price;

    return Html(Page("Checkout", $"""
        <h1>Checkout</h1>
        <p>Items: <span data-testid="checkout-count">{basket.Length}</span></p>
        <p>Basket total: <span data-testid="checkout-basket-total">{total.ToString("C", CultureInfo.GetCultureInfo("en-GB"))}</span></p>
        <p>To pay: <span data-testid="checkout-total">{charged.ToString("C", CultureInfo.GetCultureInfo("en-GB"))}</span></p>
        """));
});

// A page that asks the browser to fetch somewhere it must not reach. Nothing in the shop links to
// it; it exists so a test can prove the proxy catches a page's own fetch, not just a navigation.
app.MapGet("/probe", (string? target) => Html(Page("Probe", $"""
    <h1>Probe</h1>
    <p data-testid="probe-result">waiting</p>
    <script>
      fetch({System.Text.Json.JsonSerializer.Serialize(target ?? "http://169.254.169.254/latest/meta-data/")})
        .then(r => r.text())
        .then(t => document.querySelector('[data-testid=probe-result]').textContent = 'reached: ' + t.length)
        .catch(e => document.querySelector('[data-testid=probe-result]').textContent = 'refused');
    </script>
    """)));

app.MapGet("/health", () => Results.Text("ok"));

app.Run();

static IResult Html(string body) => Results.Content(body, "text/html; charset=utf-8");

static string Page(string title, string body) => $"""
    <!DOCTYPE html>
    <html lang="en">
    <head><meta charset="utf-8" /><title>{title} — Demo shop</title></head>
    <body>{body}</body>
    </html>
    """;

static IEnumerable<string> Read(HttpContext context) =>
    context.Request.Cookies.TryGetValue("demoshop.basket", out var value) && value.Length > 0
        ? value.Split(',', StringSplitOptions.RemoveEmptyEntries)
        : [];

static int Count(HttpContext context) => Read(context).Count();
