using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Benchmark;

/// <summary>
/// A target, started in this process on a port of its own: the booking clinic or the invoice desk. Its
/// defects and its state are set from here, so the benchmark needs no HTTP call of its own to reach them.
/// </summary>
internal sealed class InProcessHost : IDisposable
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
    public static InProcessHost Start(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        map(app);
        app.StartAsync().GetAwaiter().GetResult();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The target does not report its address.");

        return new InProcessHost(app, addresses.Addresses.First().TrimEnd('/'));
    }

    public void Dispose()
    {
        _app.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_app).Dispose();
    }
}
