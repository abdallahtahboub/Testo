using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Testo.Platform.Dashboard;
using Testo.Platform.Navigation;

namespace Testo.Platform.Modules;

/// <summary>
/// The single public entry point of a feature module. The host registers each module with
/// one line and never needs to know what is inside it.
/// </summary>
public interface IFeatureModule
{
    /// <summary>Stable, lowercase identifier, for example <c>planning</c>.</summary>
    string Id { get; }

    /// <summary>
    /// The flag that switches the module on, for example <c>Module.Planning</c>.
    /// <see langword="null"/> means the module is always on.
    /// </summary>
    string? FeatureFlag { get; }

    /// <summary>The assembly that holds the module's pages, so the router can find them.</summary>
    Assembly Assembly => GetType().Assembly;

    /// <summary>Registers the module's own services.</summary>
    void RegisterServices(IServiceCollection services)
    {
    }

    /// <summary>Adds the module's entries to the app navigation.</summary>
    void RegisterNavigation(INavigationRegistry navigation)
    {
    }

    /// <summary>Adds the module's widgets to the dashboard.</summary>
    void RegisterWidgets(IDashboardWidgetRegistry widgets)
    {
    }
}
