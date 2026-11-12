using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Testo.E2E.Support;

[assembly: AssemblyFixture(typeof(AppFixture))]

namespace Testo.E2E.Support;

/// <summary>
/// Starts the application and one browser for the whole test run. Every test opens its own
/// browser context, so tests share nothing (no cookies, no storage) and can run in parallel.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    /// <summary>
    /// Optional path to a Chromium executable. Unset, Playwright uses the browser installed
    /// by its own install script (see CLAUDE.md, Commands).
    /// </summary>
    public const string BrowserPathVariable = "E2E_BROWSER_PATH";

    private readonly TestoWebApplication _application = new();
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>The address the application listens on, for example <c>http://127.0.0.1:41873</c>.</summary>
    public Uri BaseAddress { get; private set; } = new("http://127.0.0.1");

    /// <summary>The shared browser.</summary>
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("The browser has not been started.");

    /// <summary>Turns a site-relative path into an absolute URL.</summary>
    public string Url(string path) => new Uri(BaseAddress, path).AbsoluteUri;

    /// <summary>
    /// Creates a plain HTTP client for tests that look at raw responses. It neither follows
    /// redirects nor keeps cookies, so a test sees exactly what the server answered.
    /// </summary>
    public HttpClient CreateHttpClient() =>
        new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
        {
            BaseAddress = BaseAddress,
        };

    public async ValueTask InitializeAsync()
    {
        _application.UseKestrel(0);
        _application.StartServer();

        var addresses = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server does not report its addresses.");
        BaseAddress = new Uri(addresses.Addresses.First());

        _playwright = await Playwright.CreateAsync();
        _browser = await LaunchBrowserAsync(_playwright);
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = Environment.GetEnvironmentVariable(BrowserPathVariable) is { Length: > 0 } path ? path : null,
            });
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("Executable doesn't exist", StringComparison.Ordinal))
        {
            // Playwright's own message tells the reader to run a PowerShell script. Point at
            // the way that works in this repository on every platform instead.
            throw new InvalidOperationException(
                """
                The browser these tests drive is not installed. Install it once per machine, from the repository root:

                    dotnet build tests/Testo.E2E -t:InstallTestBrowser

                That needs nothing beyond the .NET SDK. See the "Tests" section of README.md.
                """,
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        await _application.DisposeAsync();
    }
}
