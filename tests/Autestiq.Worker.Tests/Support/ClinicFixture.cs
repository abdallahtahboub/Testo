extern alias clinic;

using clinic::Autestiq.BookingShop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>
/// The booking clinic, on a real port, for the tests that explore it.
/// </summary>
/// <remarks>
/// The second application of ours (ADR-0020). It differs from the demo shop in the way that matters to
/// an exploration: its diary is kept on the server, so clearing the browser's cookies does not empty
/// it, and anything that presses its controls has to put it back first.
/// </remarks>
public sealed class ClinicFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Where the clinic is, for example <c>http://127.0.0.1:41873</c>.</summary>
    public string Origin { get; private set; } = "";

    /// <summary>The same place by name, which is how an application that is not ours would be addressed.</summary>
    public string OriginByName => Origin.Replace("127.0.0.1", "localhost", StringComparison.Ordinal);

    /// <summary>Empties the diary, so that the next visit finds the clinic as it opened.</summary>
    public static Task StartAfreshAsync()
    {
        Clinic.Reset();
        return Task.CompletedTask;
    }

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        BookingShopRoutes.Map(_app);
        await _app.StartAsync().ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The clinic does not report its address.");

        Origin = addresses.Addresses.First().TrimEnd('/');
        Defects.Set([]);
        Clinic.Reset();
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
