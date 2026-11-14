using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(Testo.Worker.Tests.Support.DemoShopFixture))]

namespace Testo.Worker.Tests.Support;

/// <summary>
/// The demo shop, on a real port, for the whole test run.
/// </summary>
/// <remarks>
/// A real port rather than a test server, because a browser has to reach it over the network the
/// way a browser does. It is ours, which is why the slice can be built and tested without aiming
/// a browser at anybody else's website (<c>docs/execution-threat-model.md</c> §4.2).
/// </remarks>
public sealed class DemoShopFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // DisposeAsync comes from WebApplicationFactory and satisfies IAsyncLifetime as it stands.

    /// <summary>Where the shop is, for example <c>http://127.0.0.1:41873</c>.</summary>
    public string Origin { get; private set; } = "";

    /// <summary>Where the browser is, when the environment keeps it somewhere Playwright would not look.</summary>
    public static string? BrowserPath =>
        Environment.GetEnvironmentVariable(ScenarioRunner.BrowserPathVariable)
        ?? Environment.GetEnvironmentVariable("E2E_BROWSER_PATH");

    public ValueTask InitializeAsync()
    {
        this.UseKestrel(0);
        StartServer();

        var addresses = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The demo shop does not report its address.");

        Origin = addresses.Addresses.First().TrimEnd('/');

        // The worker reads this to find a browser; this environment may keep one elsewhere.
        if (BrowserPath is { Length: > 0 } path)
        {
            Environment.SetEnvironmentVariable(ScenarioRunner.BrowserPathVariable, path);
        }

        return ValueTask.CompletedTask;
    }
}
