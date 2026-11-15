using System.Text;
using System.Text.RegularExpressions;
using Autestiq.DemoShop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// The demo shop, served with every <c>data-testid</c> removed: one of our own applications made to look like
/// most of the world's, which have none (ADR-0024).
/// </summary>
public sealed partial class ShopWithoutTestIdsFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Where the shop is.</summary>
    public string Origin { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.Use(StripAsync);
        DemoShopRoutes.Map(_app);
        await _app.StartAsync().ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The shop does not report its address.");

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

    private static async Task StripAsync(HttpContext context, RequestDelegate next)
    {
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;

        if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(buffer, Encoding.UTF8);
            var bytes = Encoding.UTF8.GetBytes(TestId().Replace(await reader.ReadToEndAsync().ConfigureAwait(false), ""));
            context.Response.ContentLength = bytes.Length;
            await original.WriteAsync(bytes).ConfigureAwait(false);
        }
        else
        {
            await buffer.CopyToAsync(original).ConfigureAwait(false);
        }
    }

    [GeneratedRegex(@"\s+data-testid=""[^""]*""")]
    private static partial Regex TestId();
}
