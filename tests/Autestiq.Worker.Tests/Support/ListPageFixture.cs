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
            <p><a href="/basket">Basket</a></p>
            </main></body></html>
            """,
            "text/html"));

        // A basket laid out with bare divs, as eShopOnWeb's is: the text a test checks sits in leaf divs, in no cell,
        // item or paragraph.
        _app.MapGet("/basket", () => Results.Content(
            """
            <!doctype html><html lang="en"><head><title>Basket</title></head><body><main>
            <h1>Basket</h1>
            <article class="row"><div class="col">Roslyn Red Sheet</div><div class="col">$ 8.50</div></article>
            <article class="row"><div class="col">Prism White TShirt</div><div class="col">$ 12.00</div></article>
            <div class="total">Total: $ 20.50</div>
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
