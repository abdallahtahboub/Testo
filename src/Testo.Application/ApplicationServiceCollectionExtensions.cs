using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testo.Application.Audit;
using Testo.Application.Identity;
using Testo.Contracts.Identity;
using Testo.Persistence;

namespace Testo.Application;

/// <summary>Registers the use cases and the database they run against.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the real implementations of the <c>Testo.Contracts</c> interfaces. This is the line
    /// that replaces <c>AddTestoMocks()</c> in the host (ADR-0014).
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="connectionString">Where PostgreSQL is.</param>
    public static IServiceCollection AddTestoApplication(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTestoPersistence(connectionString);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SignInWorkflow>();
        services.AddScoped<PasswordResetWorkflow>();
        services.AddScoped<SessionValidator>();

        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IAuditLog, AuditLog>();

        // Replaced by a real sender when one is chosen; until then the link goes to the log.
        services.TryAddScoped<IInvitationNotifier, LoggingInvitationNotifier>();

        return services;
    }
}
