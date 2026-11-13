using Microsoft.Playwright;

namespace Testo.E2E.Support;

/// <summary>Base class for tests that drive the application through a real browser.</summary>
public abstract class BrowserTest(AppFixture app) : IAsyncLifetime
{
    private readonly List<IBrowserContext> _contexts = [];

    /// <summary>The running application and the shared browser.</summary>
    protected AppFixture App { get; } = app;

    /// <summary>
    /// Opens a page in a fresh browser context, as a first-time visitor: no cookies, an
    /// English browser, the light color scheme and a desktop-sized window unless the test
    /// asks for something else.
    /// </summary>
    protected async Task<TestPage> OpenAsync(string path, Action<BrowserNewContextOptions>? configure = null)
    {
        var page = await NewPageAsync(configure);
        await page.Page.GotoAsync(App.Url(path));
        return page;
    }

    /// <summary>
    /// Opens a page as somebody who is already signed in as the owner of the seeded organization.
    /// Use this for anything under <c>/app</c>, which requires a session (D-1).
    /// </summary>
    protected async Task<TestPage> OpenSignedInAsync(string path, Action<BrowserNewContextOptions>? configure = null)
    {
        var page = await NewPageAsync(options =>
        {
            options.StorageStatePath = App.SignedInStateFile;
            configure?.Invoke(options);
        });

        await page.Page.GotoAsync(App.Url(path));
        return page;
    }

    /// <summary>Creates a page in a fresh browser context without navigating anywhere yet.</summary>
    protected async Task<TestPage> NewPageAsync(Action<BrowserNewContextOptions>? configure = null)
    {
        var options = new BrowserNewContextOptions
        {
            Locale = "en-US",
            ColorScheme = ColorScheme.Light,
            ReducedMotion = ReducedMotion.Reduce,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        };
        configure?.Invoke(options);

        var context = await App.Browser.NewContextAsync(options);
        context.SetDefaultTimeout(15_000);
        _contexts.Add(context);

        return new TestPage(await context.NewPageAsync(), App.BaseAddress);
    }

    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public virtual async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
