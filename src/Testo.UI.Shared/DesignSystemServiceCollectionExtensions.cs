using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;
using Testo.UI.Shared.Theming;

namespace Testo.UI.Shared;

/// <summary>Registers the services the design system needs.</summary>
public static class DesignSystemServiceCollectionExtensions
{
    /// <summary>Adds the design system: theme state and the Fluent UI component services.</summary>
    public static IServiceCollection AddTestoDesignSystem(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFluentUIComponents();
        services.TryAddScoped<ThemeState>();
        return services;
    }
}
