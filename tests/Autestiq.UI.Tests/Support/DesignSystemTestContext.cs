using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Platform.Navigation;
using Autestiq.UI.Shared;
using Autestiq.UI.Shared.Branding;
using Autestiq.UI.Shared.Theming;
using Autestiq.Website;

namespace Autestiq.UI.Tests.Support;

/// <summary>
/// Base class for component tests. It registers what the design system components need:
/// the real resource files (so tests see the real English and German texts), the brand
/// options, the theme state and a fixed clock.
/// </summary>
public abstract class DesignSystemTestContext : BunitContext
{
    /// <summary>The moment the fixed clock shows.</summary>
    protected static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 30, 0, TimeSpan.Zero);

    private readonly CultureScope _culture = new("en");

    protected DesignSystemTestContext()
    {
        Services.AddLocalization();
        Services.AddAutestiqDesignSystem();
        Services.Configure<BrandOptions>(options => options.ProductName = "Acme QA");
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

        // The real site menu, not a fake: the chrome's job is to render what it is given, and a
        // test against an invented two-item menu would not notice the real one breaking.
        Services.AddSingleton<ISiteMenu, SiteMenuCatalogue>();

        // Fluent UI components call into JavaScript when they render; the tests do not care.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>The theme state the components under test read.</summary>
    protected ThemeState Theme => Services.GetRequiredService<ThemeState>();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _culture.Dispose();
        }

        base.Dispose(disposing);
    }
}
