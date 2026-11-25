using Autestiq.Domain.Common;

namespace Autestiq.Domain.Projects;

/// <summary>
/// One address of the application a project tests, such as its staging site (Milestone 5). Runs are aimed at an
/// environment; whether they may reach it is the organization's proof of control of its origin (ADR-0026, 3.5),
/// which is kept per origin, not per environment, so two projects testing one site share one proof.
/// </summary>
/// <remarks>Named <c>TestEnvironment</c> so that it never reads as <see cref="System.Environment"/>.</remarks>
public sealed class TestEnvironment : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The project it belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Its name, such as "Staging".</summary>
    public required string Name { get; set; }

    /// <summary>Its origin, normalized by <c>Origins.Normalize</c>: scheme, host and port.</summary>
    public required string Origin { get; set; }

    /// <summary>Test or production. Production is never the default.</summary>
    public Contracts.Projects.EnvironmentKind Kind { get; set; } = Contracts.Projects.EnvironmentKind.Test;

    /// <summary>
    /// When an owner authorized runs against this production environment, or <c>null</c>. Until then the dispatcher
    /// refuses any run aimed at its origin.
    /// </summary>
    public DateTimeOffset? ProductionAuthorizedAt { get; set; }

    /// <summary>The owner who authorized it.</summary>
    public Guid? ProductionAuthorizedBy { get; set; }

    /// <summary>
    /// When an Owner allowed exploring this site to press its buttons and send its forms with test values, or
    /// <c>null</c>: off by default, and never for a production site (ADR-0032). Without it a customer's site is
    /// explored read-only.
    /// </summary>
    public DateTimeOffset? ExplorationMayActAt { get; set; }

    /// <summary>Who allowed it.</summary>
    public Guid? ExplorationMayActBy { get; set; }
}
