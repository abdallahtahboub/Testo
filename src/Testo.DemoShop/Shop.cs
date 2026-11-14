using System.Globalization;

namespace Testo.DemoShop;

/// <summary>One thing for sale.</summary>
/// <param name="Id">How a form names it.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Price">What one of them costs.</param>
public sealed record Product(string Id, string Name, decimal Price);

/// <summary>One line of a basket: a product and how many.</summary>
/// <param name="Product">What it is.</param>
/// <param name="Quantity">How many.</param>
public sealed record BasketLine(Product Product, int Quantity)
{
    /// <summary>What this line costs, honestly.</summary>
    public decimal Total => Product.Price * Quantity;
}

/// <summary>The shop's goods, its basket and its arithmetic.</summary>
/// <remarks>
/// The arithmetic is where the defects live, so it is in one place and each defect is one branch
/// with a comment saying what it does. A tester reading this file can see exactly what is wrong
/// when a given defect is on, which is what makes the benchmark's "truth" trustworthy.
/// </remarks>
public static class Shop
{
    /// <summary>The cookie the basket lives in.</summary>
    public const string BasketCookie = "demoshop.basket";

    /// <summary>The cookie a signed-in person carries.</summary>
    public const string SessionCookie = "demoshop.session";

    /// <summary>The one account, so a scenario has something to sign in to.</summary>
    public const string Account = "ada@example.test";

    /// <summary>Its password. A demo shop, so this is not a secret; the platform still types it as one.</summary>
    public const string Password = "correct-horse-battery";

    /// <summary>The discount code the shop honours.</summary>
    public const string DiscountCode = "SAVE10";

    /// <summary>What that code takes off.</summary>
    public const decimal DiscountRate = 0.10m;

    /// <summary>Everything for sale.</summary>
    public static IReadOnlyList<Product> Catalogue { get; } =
    [
        new("tea", "Loose leaf tea, 200g", 8.50m),
        new("pot", "Cast iron teapot", 42.00m),
        new("cup", "Porcelain cup", 11.25m),
        new("tray", "Lacquered tray", 23.00m),
    ];

    /// <summary>Money, the way the shop writes it.</summary>
    public static string Money(decimal amount) =>
        amount.ToString("C", CultureInfo.GetCultureInfo("en-GB"));

    /// <summary>Finds a product, or <c>null</c>.</summary>
    public static Product? Find(string? id) =>
        Catalogue.FirstOrDefault(product => string.Equals(product.Id, id, StringComparison.Ordinal));

    /// <summary>The basket, as lines.</summary>
    public static IReadOnlyList<BasketLine> Basket(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ids = context.Request.Cookies.TryGetValue(BasketCookie, out var value) && value.Length > 0
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : [];

        return
        [
            .. ids
                .Select(Find)
                .Where(product => product is not null)
                .GroupBy(product => product!.Id, StringComparer.Ordinal)
                .Select(group => new BasketLine(group.First()!, group.Count())),
        ];
    }

    /// <summary>Writes the basket back, as a flat list of ids.</summary>
    public static void Keep(HttpContext context, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Cookies.Append(BasketCookie, string.Join(',', ids), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
    }

    /// <summary>The ids in the basket, in order, with repeats.</summary>
    public static IReadOnlyList<string> Ids(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Cookies.TryGetValue(BasketCookie, out var value) && value.Length > 0
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : [];
    }

    /// <summary>Whether somebody is signed in.</summary>
    public static bool SignedIn(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Cookies.ContainsKey(SessionCookie);
    }

    /// <summary>
    /// What the basket says it comes to.
    /// </summary>
    /// <remarks>
    /// With <see cref="Defects.QuantityIgnored"/>, each line is counted once however many of that
    /// item there are: two cups cost what one cup costs. A plausible bug and an expensive one.
    /// </remarks>
    public static decimal BasketTotal(IReadOnlyList<BasketLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return Defects.On(Defects.QuantityIgnored)
            ? lines.Sum(line => line.Product.Price)
            : lines.Sum(line => line.Total);
    }

    /// <summary>
    /// What the checkout actually asks for.
    /// </summary>
    /// <remarks>
    /// With <see cref="Defects.CheckoutTotal"/>, everything after the first line is left out. With
    /// <see cref="Defects.DiscountTwice"/>, an accepted code is taken off twice. Both are the kind
    /// of thing that reaches production because the basket page looks right.
    /// </remarks>
    public static decimal ToPay(IReadOnlyList<BasketLine> lines, bool discounted)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var subtotal = Defects.On(Defects.CheckoutTotal)
            ? lines.Take(1).Sum(line => line.Total)
            : BasketTotal(lines);

        if (!discounted)
        {
            return subtotal;
        }

        return Defects.On(Defects.DiscountTwice)
            ? subtotal * (1 - DiscountRate) * (1 - DiscountRate)
            : subtotal * (1 - DiscountRate);
    }

    /// <summary>
    /// Whether these are the right credentials.
    /// </summary>
    /// <remarks>
    /// With <see cref="Defects.SignInAnyPassword"/>, the password is not checked. The one defect
    /// here that is a security hole rather than a wrong number, and the one most worth catching.
    /// </remarks>
    public static bool Authenticate(string? email, string? password) =>
        string.Equals(email?.Trim(), Account, StringComparison.OrdinalIgnoreCase)
        && (Defects.On(Defects.SignInAnyPassword) || string.Equals(password, Password, StringComparison.Ordinal));

    /// <summary>
    /// The basket with one product taken out.
    /// </summary>
    /// <remarks>
    /// With <see cref="Defects.RemoveWrongItem"/>, the first line goes instead of the one asked
    /// for — which looks right whenever the basket holds one thing, and is how it survives review.
    /// </remarks>
    public static IReadOnlyList<string> Without(IReadOnlyList<string> ids, string? productId)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return ids;
        }

        var target = Defects.On(Defects.RemoveWrongItem) ? ids[0] : productId;

        var removed = false;

        return
        [
            .. ids.Where(id =>
            {
                if (!removed && string.Equals(id, target, StringComparison.Ordinal))
                {
                    removed = true;
                    return false;
                }

                return true;
            }),
        ];
    }
}
