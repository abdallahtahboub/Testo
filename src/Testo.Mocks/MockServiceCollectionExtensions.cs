using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Testo.Mocks;

/// <summary>Registers the mock implementations of the data services.</summary>
public static class MockServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory mock services. The real backend will replace this single call with
    /// the registration of the HTTP clients; nothing else in the application changes.
    /// </summary>
    public static IServiceCollection AddTestoMocks(this IServiceCollection services, Action<MockOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<MockOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<MockRandomSource>();
        return services;
    }
}
