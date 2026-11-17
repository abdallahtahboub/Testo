using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Autestiq.Application.Audit;
using Autestiq.Application.Execution;
using Autestiq.Application.Identity;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Persistence;

namespace Autestiq.Application;

/// <summary>Registers the use cases and the database they run against.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the real implementations of the <c>Autestiq.Contracts</c> interfaces. This is the line
    /// that replaces <c>AddAutestiqMocks()</c> in the host (ADR-0014).
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="connectionString">Where PostgreSQL is.</param>
    public static IServiceCollection AddAutestiqApplication(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAutestiqPersistence(connectionString);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SignInWorkflow>();
        services.AddScoped<PasswordResetWorkflow>();
        services.AddScoped<SessionValidator>();

        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IExecutionService, ExecutionService>();
        services.AddScoped<IOriginProofService, OriginProofService>();
        services.AddScoped<Contracts.Projects.IProjectService, Projects.ProjectService>();
        services.AddScoped<Contracts.Usage.IUsageService, Usage.UsageService>();

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
    /// <see cref="AddAutestiqApplication"/> so that a test can use the application without one
    /// starting behind it.
    /// </summary>
    public static IServiceCollection AddAutestiqRunDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<RunDispatcher>();
        return services;
    }
}
