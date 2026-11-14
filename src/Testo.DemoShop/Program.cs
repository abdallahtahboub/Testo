using Testo.DemoShop;

// A deliberately small web shop for test runs to act on.
//
// It is not a demo of Testo. It is the thing Testo points at, and it exists so that execution can
// be built and measured without aiming a browser at anybody else's website
// (docs/execution-threat-model.md §4.2).
//
// Several of its features are wrong on purpose, one defect at a time, switched through /_defects.
// That switch is the instrument the AI benchmark rests on: a tester that always says "passed"
// scores perfectly against a working application, so the only measurement worth having is whether
// it still says "passed" when something is actually broken (Defects.cs).

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();

Defects.ReadEnvironment();

app.MapGet("/", (HttpContext context, string? q) =>
{
    var shown = string.IsNullOrWhiteSpace(q)
        ? Shop.Catalogue
        : [.. Shop.Catalogue.Where(product => product.Name.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase))];

    var rows = string.Join("", shown.Select(product => $"""
        <li>
          <span data-testid="name-{product.Id}">{product.Name}</span>
          <span data-testid="price-{product.Id}">{Shop.Money(product.Price)}</span>
          <form method="post" action="/basket/add">
            <input type="hidden" name="id" value="{product.Id}" />
            <button type="submit" data-testid="add-{product.Id}">Add to basket</button>
          </form>
        </li>
        """));

    var search = """
        <form method="get" action="/" role="search">
          <label for="q">Search the catalogue</label>
          <input type="search" id="q" name="q" data-testid="search" placeholder="Search the catalogue" />
          <button type="submit" data-testid="do-search">Search</button>
        </form>
        """;

    var nothing = shown.Count == 0 ? "<p data-testid=\"no-results\">Nothing matches that.</p>" : "";

    return Html.Page("Catalogue", $"""
        <h1>Catalogue</h1>
        {search}
        {nothing}
        <ul class="catalogue">{rows}</ul>
        <p><a href="/basket" data-testid="to-basket">Basket ({Shop.Basket(context).Sum(line => line.Quantity)})</a></p>
        {Html.Account(context)}
        <footer><a href="https://example.com/supplier">Our supplier</a></footer>
        """);
});

app.MapPost("/basket/add", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    if (Shop.Find(form["id"].ToString()) is { } product)
    {
        Shop.Keep(context, [.. Shop.Ids(context), product.Id]);
    }

    return Results.Redirect("/basket");
});

app.MapPost("/basket/remove", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    Shop.Keep(context, Shop.Without(Shop.Ids(context), form["id"].ToString()));

    return Results.Redirect("/basket");
});

app.MapGet("/basket", (HttpContext context) =>
{
    var lines = Shop.Basket(context);

    if (lines.Count == 0)
    {
        return Html.Page("Basket", """
            <h1>Basket</h1>
            <p data-testid="empty">Your basket is empty.</p>
            <p><a href="/" data-testid="to-catalogue">Back to the catalogue</a></p>
            """);
    }

    var rows = string.Join("", lines.Select(line => $"""
        <li data-testid="line-{line.Product.Id}">
          <span>{line.Product.Name}</span>
          <span data-testid="quantity-{line.Product.Id}">{line.Quantity}</span>
          <span data-testid="line-total-{line.Product.Id}">{Shop.Money(line.Total)}</span>
          <form method="post" action="/basket/remove">
            <input type="hidden" name="id" value="{line.Product.Id}" />
            <button type="submit" data-testid="remove-{line.Product.Id}">Remove</button>
          </form>
        </li>
        """));

    return Html.Page("Basket", $"""
        <h1>Basket</h1>
        <ul data-testid="lines">{rows}</ul>
        <p>Total: <span data-testid="basket-total">{Shop.Money(Shop.BasketTotal(lines))}</span></p>
        <p><a href="/checkout" data-testid="to-checkout">Checkout</a></p>
        <p><a href="/" data-testid="to-catalogue">Back to the catalogue</a></p>
        """);
});

app.MapGet("/checkout", (HttpContext context, string? code) =>
{
    var lines = Shop.Basket(context);
    var discounted = string.Equals(code?.Trim(), Shop.DiscountCode, StringComparison.OrdinalIgnoreCase);

    var discountRow = code is { Length: > 0 }
        ? $"<p>Discount code: <span data-testid=\"discount-state\">{(discounted ? "accepted" : "not a code we know")}</span></p>"
        : "";

    var order = lines.Count == 0 && !Defects.On(Defects.EmptyOrderAllowed)
        ? "<p data-testid=\"nothing-to-pay\">There is nothing in your basket to pay for.</p>"
        : """
          <form method="post" action="/order">
            <button type="submit" data-testid="place-order">Place the order</button>
          </form>
          """;

    return Html.Page("Checkout", $"""
        <h1>Checkout</h1>
        <form method="get" action="/checkout">
          <label for="code">Discount code</label>
          <input type="text" id="code" name="code" data-testid="code" />
          <button type="submit" data-testid="apply-code">Apply</button>
        </form>
        {discountRow}
        <p>Items: <span data-testid="checkout-count">{lines.Sum(line => line.Quantity)}</span></p>
        <p>Basket total: <span data-testid="checkout-basket-total">{Shop.Money(Shop.BasketTotal(lines))}</span></p>
        <p>To pay: <span data-testid="checkout-total">{Shop.Money(Shop.ToPay(lines, discounted))}</span></p>
        {order}
        <p><a href="/basket" data-testid="to-basket">Back to the basket</a></p>
        """);
});

app.MapPost("/order", (HttpContext context) =>
{
    var lines = Shop.Basket(context);

    if (lines.Count == 0 && !Defects.On(Defects.EmptyOrderAllowed))
    {
        return Results.Redirect("/checkout");
    }

    Shop.Keep(context, []);

    return Results.Redirect("/order/placed");
});

app.MapGet("/order/placed", () => Html.Page("Order placed", """
    <h1>Order placed</h1>
    <p data-testid="order-state">Thank you. Your order is on its way.</p>
    <p><a href="/" data-testid="to-catalogue">Back to the catalogue</a></p>
    """));

app.MapGet("/sign-in", (string? failed) => Html.Page("Sign in", $"""
    <h1>Sign in</h1>
    {(failed is not null ? "<p data-testid=\"sign-in-error\">Those are not the right details.</p>" : "")}
    <form method="post" action="/sign-in">
      <label for="email">Email</label>
      <input type="email" id="email" name="email" data-testid="email" />
      <label for="password">Password</label>
      <input type="password" id="password" name="password" data-testid="password" />
      <button type="submit" data-testid="do-sign-in">Sign in</button>
    </form>
    <p><a href="/" data-testid="to-catalogue">Back to the catalogue</a></p>
    """));

app.MapPost("/sign-in", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    if (!Shop.Authenticate(form["email"].ToString(), form["password"].ToString()))
    {
        return Results.Redirect("/sign-in?failed=1");
    }

    context.Response.Cookies.Append(Shop.SessionCookie, Shop.Account, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
    });

    return Results.Redirect("/account");
});

app.MapGet("/account", (HttpContext context) => Shop.SignedIn(context)
    ? Html.Page("Account", $"""
        <h1>Account</h1>
        <p data-testid="signed-in-as">Signed in as {Shop.Account}</p>
        <p><a href="/" data-testid="to-catalogue">Back to the catalogue</a></p>
        """)
    : Html.Page("Account", """
        <h1>Account</h1>
        <p data-testid="not-signed-in">You are not signed in.</p>
        <p><a href="/sign-in" data-testid="to-sign-in">Sign in</a></p>
        """));

// Which defects the shop currently has. Not linked from any page: it is a control for whoever is
// running the benchmark, in the same spirit as a test hook on a staging environment.
app.MapGet("/_defects", () => Results.Json(new { on = Defects.Current, known = Defects.All }));

app.MapPost("/_defects", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var wanted = form["defects"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A name nobody recognizes is refused rather than ignored: a typo that silently meant "no
    // defects" would flatter every result measured afterwards.
    return Defects.Set(wanted)
        ? Results.Json(new { on = Defects.Current })
        : Results.BadRequest(new { error = "unknown defect", known = Defects.All });
});

// A page that asks the browser to fetch somewhere it must not reach. Nothing in the shop links to
// it; it exists so a test can prove the proxy catches a page's own fetch, not just a navigation.
app.MapGet("/probe", (string? target) => Html.Page("Probe", $$"""
    <h1>Probe</h1>
    <p data-testid="probe-result">waiting</p>
    <script>
      fetch({{System.Text.Json.JsonSerializer.Serialize(target ?? "http://169.254.169.254/latest/meta-data/")}})
        .then(r => r.text())
        .then(t => document.querySelector('[data-testid=probe-result]').textContent = 'reached: ' + t.length)
        .catch(e => document.querySelector('[data-testid=probe-result]').textContent = 'refused');
    </script>
    """));

// Sends the browser somewhere it must not go. Nothing links to it; it exists so a test can prove
// that a redirect is judged at the hop it redirects to, not only at the address first asked for.
app.MapGet("/redirect", (string? to) => Results.Redirect(to ?? "http://169.254.169.254/latest/meta-data/"));

app.MapGet("/health", () => Results.Text("ok"));

app.Run();
