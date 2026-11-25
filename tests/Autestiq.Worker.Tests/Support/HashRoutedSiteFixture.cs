using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// A client-side application in miniature, shaped as OWASP Juice Shop is (R9): one document whose pages are routes in
/// the fragment (<c>/#/about</c>), each drawn by the page's own script well after the load event.
/// </summary>
public sealed class HashRoutedSiteFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Where the application is.</summary>
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
            ?? throw new InvalidOperationException("The application does not report its address.");

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

    // The application's own markup and script, not ours. Nothing is in <main> until the router has drawn it.
    private const string Page =
        """
        <!doctype html><html lang="en"><head><title>Orchard</title></head><body>
        <nav><a href="#/about">About</a> <a href="#/contact">Contact</a> <a href="#main">Skip to the content</a></nav>
        <main id="main"></main>
        <script>
          const pages = {
            '#/about': '<h1>About the orchard</h1><p>Founded in 2014</p>',
            '#/contact': '<h1>Write to us</h1><p>We answer within a day</p>',
          };
          function draw() {
            setTimeout(() => {
              document.getElementById('main').innerHTML = pages[location.hash] ?? '<h1>Welcome</h1><p>Fresh apples daily</p>';
            }, 600);
          }
          window.addEventListener('hashchange', draw);
          draw();
        </script>
        </body></html>
        """;
}
