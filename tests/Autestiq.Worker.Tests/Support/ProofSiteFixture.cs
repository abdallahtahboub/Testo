using Autestiq.Contracts.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autestiq.Worker.Tests.Support;

/// <summary>A site whose proof file says whatever a test sets, or is missing when it sets nothing.</summary>
public sealed class ProofSiteFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Where the site is.</summary>
    public string Origin { get; private set; } = "";

    /// <summary>What the proof file holds; <c>null</c> for no file at all.</summary>
    public string? Proof { get; set; }

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.MapGet(OriginProofFile.Path, () => Proof is { } proof ? Results.Text(proof) : Results.NotFound());
        await _app.StartAsync().ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The site does not report its address.");

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
