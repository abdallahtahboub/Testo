using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// Pages with what our three applications lack and their frozen scenario sets cannot add: a list, for the select
/// action, and a basket laid out with bare divs, for what the explorer reads as text.
/// </summary>
public sealed class ListPageFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Where the pages are.</summary>
    public string Origin { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/", () => Results.Content(
            """
            <!doctype html><html lang="en"><head><title>Catalogue</title></head><body><main>
            <h1>Catalogue</h1>
            <form method="get" action="/chosen">
              <label for="brand">Brand</label>
              <select id="brand" name="brand"><option>All</option><option>.NET</option><option>Azure</option></select>
              <button type="submit">Show</button>
            </form>
            <p><a href="/basket">Basket</a> <a href="/shop">Shop</a></p>
            </main></body></html>
            """,
            "text/html"));

        // A basket laid out as eShopOnWeb's is (its Pages/Basket/Index.cshtml): each line an article holding a div of
        // bare sections, the text a test checks in leaf sections, in no cell, item or paragraph; and a total in a bare div.
        _app.MapGet("/basket", () => Results.Content(
            """
            <!doctype html><html lang="en"><head><title>Basket</title></head><body><main>
            <h1>Basket</h1>
            <article class="row"><div><section class="col">Roslyn Red Sheet</section><section class="col">$ 8.50</section></div></article>
            <article class="row"><div><section class="col">Prism White TShirt</section><section class="col">$ 12.00</section></div></article>
            <div class="total">Total: $ 20.50</div>
            </main></body></html>
            """,
            "text/html"));

        // Five products, each with a button that opens a basket holding it: one kind of page that says something
        // different for every product.
        string[] products = ["Teapot", "Cup", "Tray", "Saucer", "Spoon"];

        _app.MapGet("/shop", () => Results.Content(
            "<!doctype html><html lang=\"en\"><head><title>Shop</title></head><body><main><h1>Shop</h1>"
            + string.Concat(products.Select(product =>
                $"<article><h2>{product}</h2><form method=\"get\" action=\"/added\"><input type=\"hidden\" name=\"item\" value=\"{product}\"><button type=\"submit\">Add</button></form></article>"))
            + "</main></body></html>",
            "text/html"));

        _app.MapGet("/added", (string? item) => Results.Content(
            $"""
            <!doctype html><html lang="en"><head><title>Basket</title></head><body><main>
            <h1>Your basket</h1>
            <article><div><section>{WebUtility.HtmlEncode(item ?? "nothing")}</section><section>1</section></div></article>
            </main></body></html>
            """,
            "text/html"));

        _app.MapGet("/chosen", (string? brand) => Results.Content(
            $"""
            <!doctype html><html lang="en"><head><title>Chosen</title></head><body><main>
            <h1>Chosen</h1><p>You chose {WebUtility.HtmlEncode(brand ?? "nothing")}</p>
            </main></body></html>
            """,
            "text/html"));

        await _app.StartAsync().ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The pages do not report their address.");

        Origin = addresses.Addresses.First().TrimEnd('/');
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
