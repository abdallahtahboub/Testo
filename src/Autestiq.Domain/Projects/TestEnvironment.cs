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
}
