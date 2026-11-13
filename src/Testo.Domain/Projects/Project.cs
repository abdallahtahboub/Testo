using Testo.Domain.Common;

namespace Testo.Domain.Projects;

/// <summary>
/// A product under test. Not a customer — a customer is an
/// <see cref="Identity.Organization"/>, and may be testing several products.
/// </summary>
public sealed class Project : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The name people use for it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The short form that appears in every key this project hands out, as in the <c>SHOP</c> of
    /// <c>SHOP-REQ-1042</c>. Unique within the organization; the counters behind it run per
    /// project (<c>docs/domain-model.md</c> §7, question 1).
    /// </summary>
    public required string KeyPrefix { get; set; }

    /// <summary>What it is, for people arriving later.</summary>
    public string? Description { get; set; }

    /// <summary>Whether it is still worked on.</summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
}

/// <summary>Whether a project is still worked on.</summary>
public enum ProjectStatus
{
    /// <summary>In normal use.</summary>
    Active = 1,

    /// <summary>Out of the way, still reachable from history.</summary>
    Archived = 2,
}
