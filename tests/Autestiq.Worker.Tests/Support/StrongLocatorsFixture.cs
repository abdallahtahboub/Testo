using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// A page with what the vocabulary could not name before ADR-0042: fields whose role is not nameable (a number, a date),
/// reached by their labels, beside password fields, which the browser gives the textbox role; a menu
/// and tabs, which are roles and not elements; and the same control twice, told apart only by the dialog, navigation
/// or footer it sits in.
/// </summary>
public sealed class StrongLocatorsFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Where the page is.</summary>
    public string Origin { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.MapGet("/", () => Results.Content(Page, "text/html"));

        await _app.StartAsync().ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The page does not report its address.");

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

    private const string Page =
        """
        <!doctype html><html lang="en"><head><title>Account</title></head><body>
        <header><nav aria-label="Main"><a href="/">Home</a> <a href="/">Sign in</a></nav></header>
        <main>
          <h1>Account</h1>
          <form>
            <label for="email">Email</label><input id="email" type="email">
            <label for="password">Password</label><input id="password" type="password">
            <input type="password" aria-label="Repeat password">
            <label for="quantity">Quantity</label><input id="quantity" type="number">
            <label for="date">Delivery date</label><input id="date" type="date">
          </form>
          <div role="menu" aria-label="Account menu"><div role="menuitem">Settings</div><div role="menuitem">Billing</div></div>
          <div role="tablist"><button role="tab">Overview</button><button role="tab">History</button></div>
          <div role="dialog" aria-label="Cookies"><span>We use cookies</span> <button>Close</button></div>
          <div><button>Close</button></div>
        </main>
        <footer><span>Orchard Ltd</span> <a href="/">Sign in</a></footer>
        </body></html>
        """;
}
