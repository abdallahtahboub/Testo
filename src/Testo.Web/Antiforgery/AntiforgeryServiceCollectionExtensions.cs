using Microsoft.AspNetCore.Antiforgery;

namespace Testo.Web.Antiforgery;

internal static class AntiforgeryServiceCollectionExtensions
{
    /// <summary>
    /// Wraps the framework's antiforgery service in <see cref="PublicPageAntiforgery"/>.
    /// Call it after the services that register antiforgery (Razor components).
    /// </summary>
    public static IServiceCollection AddPublicPageAntiforgery(this IServiceCollection services)
    {
        services.AddAntiforgery();

        var original = services.Last(descriptor => descriptor.ServiceType == typeof(IAntiforgery));
        services.Remove(original);
        services.AddSingleton<IAntiforgery>(provider => new PublicPageAntiforgery(CreateOriginal(provider, original)));
        return services;
    }

    private static IAntiforgery CreateOriginal(IServiceProvider provider, ServiceDescriptor original)
    {
        if (original.ImplementationInstance is IAntiforgery instance)
        {
            return instance;
        }

        if (original.ImplementationFactory is not null)
        {
            return (IAntiforgery)original.ImplementationFactory(provider);
        }

        var type = original.ImplementationType
            ?? throw new InvalidOperationException("The antiforgery service is registered in an unexpected way.");
        return (IAntiforgery)ActivatorUtilities.CreateInstance(provider, type);
    }
}
