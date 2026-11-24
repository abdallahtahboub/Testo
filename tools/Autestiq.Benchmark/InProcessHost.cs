using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Benchmark;

/// <summary>
/// A target, started in this process on a port of its own: the demo shop, the booking clinic or the
/// invoice desk. Its defects and its state are set from here, so the benchmark needs no HTTP call of its
/// own to reach them.
/// </summary>
internal sealed partial class InProcessHost : IDisposable
{
    private readonly WebApplication _app;

    private InProcessHost(WebApplication app, string origin)
    {
        _app = app;
        Origin = origin;
    }

    /// <summary>Where it is listening.</summary>
    public string Origin { get; }

    /// <summary>Starts it on a free port, with the routes <paramref name="map"/> adds.</summary>
    /// <param name="map">Adds the target's routes.</param>
    /// <param name="withoutTestIds">
    /// Serves every page with its <c>data-testid</c> attributes removed: one of our own applications made
    /// to look like most of the world's, which have none. For developing what the product does without
    /// them, without spending the one application nobody here wrote on it.
    /// </param>
    /// <param name="labels">The target's control labels for <see cref="AppChanges.Relabel"/>; none when the lab changes nothing.</param>
    public static InProcessHost Start(Action<WebApplication> map, bool withoutTestIds = false, IReadOnlyDictionary<string, string>? labels = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();

        if (withoutTestIds)
        {
            app.Use(StripTestIdsAsync);
        }

        // The version change of Experiment 0001, applied only while the lab sets one. Registered after stripping test
        // ids, so it runs first on the way out, and with no change in force it leaves every page as it was.
        app.Use((context, next) => AppChanges.Current is { } change
            ? RewriteHtmlAsync(context, next, html => AppChanges.Apply(html, change, labels ?? AppChanges.LabelsFor("demo")))
            : next(context));

        map(app);
        app.StartAsync().GetAwaiter().GetResult();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The target does not report its address.");

        return new InProcessHost(app, addresses.Addresses.First().TrimEnd('/'));
    }

    /// <summary>Removes every <c>data-testid</c> attribute from a page on its way out. Nothing else changes.</summary>
    private static Task StripTestIdsAsync(HttpContext context, RequestDelegate next) =>
        RewriteHtmlAsync(context, next, html => TestIdAttribute().Replace(html, ""));

    /// <summary>Rewrites an HTML page on its way out; anything else passes untouched.</summary>
    private static async Task RewriteHtmlAsync(HttpContext context, RequestDelegate next, Func<string, string> rewrite)
    {
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;

        if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(buffer, Encoding.UTF8);
            var bytes = Encoding.UTF8.GetBytes(rewrite(await reader.ReadToEndAsync()));
            context.Response.ContentLength = bytes.Length;
            await original.WriteAsync(bytes);
        }
        else
        {
            await buffer.CopyToAsync(original);
        }
    }

    [GeneratedRegex(@"\s+data-testid=""[^""]*""")]
    private static partial Regex TestIdAttribute();

    public void Dispose()
    {
        _app.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_app).Dispose();
    }
}
