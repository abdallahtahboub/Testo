using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testo.Application.Audit;
using Testo.Application.Execution;
using Testo.Application.Identity;
using Testo.Contracts.Execution;
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
        services.AddScoped<IExecutionService, ExecutionService>();

        // Artifacts: a directory today, object storage when there is more than one machine
        // (ADR-0018). Keys begin with the organization either way.
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        services.AddSingleton<ArtifactLinks>();

        // Replaced by a real sender when one is chosen; until then the link goes to the log.
        services.TryAddScoped<IInvitationNotifier, LoggingInvitationNotifier>();

        return services;
    }

    /// <summary>
    /// Adds the background service that gives queued runs to workers. Separate from
    /// <see cref="AddTestoApplication"/> so that a test can use the application without one
    /// starting behind it.
    /// </summary>
    public static IServiceCollection AddTestoRunDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<RunDispatcher>();
        return services;
    }
}
