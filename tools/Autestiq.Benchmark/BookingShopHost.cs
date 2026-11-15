using Autestiq.BookingShop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Benchmark;

/// <summary>
/// The held-out target, started in this process on a port of its own. Its defects and its diary are
/// set from here, so the benchmark needs no HTTP call of its own to reach them.
/// </summary>
internal sealed class BookingShopHost : IDisposable
{
    private readonly WebApplication _app;

    private BookingShopHost(WebApplication app, string origin)
    {
        _app = app;
        Origin = origin;
    }

    /// <summary>Where it is listening.</summary>
    public string Origin { get; }

    /// <summary>Starts it on a free port.</summary>
    public static BookingShopHost Start()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        BookingShopRoutes.Map(app);
        app.StartAsync().GetAwaiter().GetResult();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The clinic does not report its address.");

        return new BookingShopHost(app, addresses.Addresses.First().TrimEnd('/'));
    }

    public void Dispose()
    {
        _app.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_app).Dispose();
    }
}
