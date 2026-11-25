using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Autestiq.Platform.Navigation;

namespace Autestiq.Platform.Modules;

/// <summary>Host-side registration of feature modules.</summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers a feature module and its services. This one line per module is all the host
    /// needs; the registered modules are available as <c>IEnumerable&lt;IFeatureModule&gt;</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A module with the same id is already registered.</exception>
    public static IServiceCollection AddAutestiqModule<TModule>(this IServiceCollection services)
        where TModule : class, IFeatureModule, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        var module = new TModule();
        var duplicate = services
            .Where(descriptor => descriptor.ServiceType == typeof(IFeatureModule))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IFeatureModule>()
            .FirstOrDefault(registered => string.Equals(registered.Id, module.Id, StringComparison.OrdinalIgnoreCase));

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"A module with the id '{module.Id}' is already registered ({duplicate.GetType().FullName}). Module ids must be unique.");
        }

        services.AddSingleton<IFeatureModule>(module);
        module.RegisterServices(services);

        // Every module's navigation, collected once from all of them; the shell filters it by feature flag.
        services.TryAddSingleton<INavigationRegistry>(provider => NavigationRegistry.From(provider.GetServices<IFeatureModule>()));
        return services;
    }
}
