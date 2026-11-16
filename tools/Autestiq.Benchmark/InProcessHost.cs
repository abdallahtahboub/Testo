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
    public static InProcessHost Start(Action<WebApplication> map, bool withoutTestIds = false)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();

        if (withoutTestIds)
        {
            app.Use(StripTestIdsAsync);
        }

        map(app);
        app.StartAsync().GetAwaiter().GetResult();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The target does not report its address.");

        return new InProcessHost(app, addresses.Addresses.First().TrimEnd('/'));
    }

    /// <summary>Removes every <c>data-testid</c> attribute from a page on its way out. Nothing else changes.</summary>
    private static async Task StripTestIdsAsync(HttpContext context, RequestDelegate next)
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
            var bytes = Encoding.UTF8.GetBytes(TestIdAttribute().Replace(await reader.ReadToEndAsync(), ""));
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
