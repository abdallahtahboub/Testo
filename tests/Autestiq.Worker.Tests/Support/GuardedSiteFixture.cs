using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// A page of controls that change things in place, as a client-side application's do, and controls that try to send
/// something by every route a browser has (ADR-0041). The server counts every request that could change data, so a test
/// checks on the server, not in the browser, that guarded pressing sent none.
/// </summary>
public sealed class GuardedSiteFixture : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _writes = new();
    private WebApplication? _app;

    /// <summary>Where the page is, by number.</summary>
    public string Origin { get; private set; } = "";

    /// <summary>The same page addressed by name, which the worker never acts on: a customer's site, as far as it knows.</summary>
    public string OriginByName => Origin.Replace("127.0.0.1", "localhost", StringComparison.Ordinal);

    /// <summary>Every request that reached the server and could have changed data, as method and path, in order.</summary>
    public IReadOnlyCollection<string> Writes => _writes;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.UseWebSockets();

        // Counted before anything answers: a write that reached the server at all is the failure.
        _app.Use(async (context, next) =>
        {
            var request = context.Request;

            if (context.WebSockets.IsWebSocketRequest)
            {
                _writes.Enqueue($"WebSocket {request.Path}");
            }
            else if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method))
            {
                _writes.Enqueue($"{request.Method} {request.Path}");
            }
            else if (request.Path.StartsWithSegments("/search", StringComparison.Ordinal))
            {
                // A form sent by GET reads, but forms are never sent under the guard, so this counts too.
                _writes.Enqueue($"GET {request.Path} (a form)");
            }

            await next(context).ConfigureAwait(false);
        });

        _app.MapGet("/", () => Results.Content(Page, "text/html"));
        _app.MapGet("/search", () => Results.Content("<!doctype html><html lang=\"en\"><head><title>Found</title></head><body><main><h1>Found</h1></main></body></html>", "text/html"));
        _app.MapMethods("/{**rest}", ["POST", "PUT", "PATCH", "DELETE"], () => Results.Ok());

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

    // The application's own markup and scripts, not ours: what a client-side page looks like to the explorer.
    private const string Page =
        """
        <!doctype html><html lang="en"><head><title>Teapot</title></head><body><main>
        <h1>Teapot</h1>
        <div role="tablist">
          <button type="button" id="tab-details" onclick="show('details')">Details</button>
          <button type="button" id="tab-reviews" onclick="show('reviews')">Reviews</button>
        </div>
        <section id="panel"><p>Holds a litre</p></section>

        <p id="said"></p>
        <button type="button" onclick="send('POST', '/api/draft')">Save draft</button>
        <button type="button" onclick="send('PUT', '/api/rating')">Rate it</button>
        <button type="button" onclick="send('PATCH', '/api/note')">Note it</button>
        <button type="button" onclick="send('DELETE', '/api/old')">Tidy up</button>
        <button type="button" onclick="navigator.sendBeacon('/api/beacon', 'seen'); said('Pinged')">Ping</button>
        <button type="button" onclick="live()">Go live</button>
        <button type="button" onclick="document.getElementById('note').submit()">Quietly</button>

        <form id="note" method="post" action="/api/form">
          <label for="text">Note</label><input id="text" name="text" value="hello">
          <button>Keep</button>
        </form>
        <form method="get" action="/search">
          <label for="q">Find</label><input id="q" name="q">
          <button type="submit">Look</button>
        </form>
        <script>
          // Rendered when the tab is chosen, as a client-side application renders it: not in the page until then.
          function show(id) {
            document.getElementById('panel').innerHTML = id === 'reviews'
              ? '<h2>What people say</h2><p>Rated 4.5 by 12 people</p>'
              : '<p>Holds a litre</p>';
          }
          function said(text) { document.getElementById('said').textContent = text; }
          function send(method, path) {
            fetch(path, { method }).then(() => said('Saved'), () => said('Could not save'));
          }
          // Live updates, as OWASP Juice Shop's are: a post every few hundred milliseconds whatever is pressed. Its block
          // is the page's, not a press's, and must not hide what a tab shows (R9).
          setInterval(() => fetch('/api/poll', { method: 'POST' }).catch(() => {}), 300);
          function live() {
            const socket = new WebSocket(location.origin.replace('http', 'ws') + '/ws');
            socket.onopen = () => said('Live');
          }
        </script>
        </main></body></html>
        """;
}
