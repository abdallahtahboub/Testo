using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;
using Testo.UI.Shared.Identity;
using Testo.UI.Shared.Localization;
using Testo.UI.Shared.Theming;

namespace Testo.UI.Shared;

/// <summary>Registers the services the design system needs.</summary>
public static class DesignSystemServiceCollectionExtensions
{
    /// <summary>Adds the design system: session state and the Fluent UI component services.</summary>
    public static IServiceCollection AddTestoDesignSystem(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFluentUIComponents();
        services.TryAddScoped<ThemeState>();
        services.TryAddScoped<SignedInUser>();

        // Lets the chrome render labels that live in another project's resource file, which is how
        // the site menu and the module navigation keep their copy to themselves.
        services.TryAddSingleton<LocalizedTextLookup>();
        return services;
    }
}
