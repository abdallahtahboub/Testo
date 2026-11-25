using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;
using Autestiq.UI.Shared.Identity;
using Autestiq.UI.Shared.Localization;
using Autestiq.UI.Shared.Theming;

namespace Autestiq.UI.Shared;

/// <summary>Registers the services the design system needs.</summary>
public static class DesignSystemServiceCollectionExtensions
{
    /// <summary>Adds the design system: session state and the Fluent UI component services.</summary>
    public static IServiceCollection AddAutestiqDesignSystem(this IServiceCollection services)
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
