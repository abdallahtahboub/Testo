using Autestiq.Domain.Common;

namespace Autestiq.Domain.Projects;

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

    /// <summary>
    /// When someone allowed AI planning to process this project's discovered page content, or <c>null</c>: off by
    /// default. Without it no page content of this project is sent to a model (ADR-0030, gap analysis G-1).
    /// </summary>
    public DateTimeOffset? AiProcessingAllowedAt { get; set; }

    /// <summary>Who allowed it.</summary>
    public Guid? AiProcessingAllowedBy { get; set; }

    /// <summary>
    /// How many days a run's screenshots are kept, one of <c>EvidenceRetention.AllowedDays</c>: 90 by default, and 0 for
    /// none kept at all. A run's result and steps are kept for good; its screenshots are customer data and go when their
    /// time is up (gap analysis G-5).
    /// </summary>
    public int EvidenceRetentionDays { get; set; } = 90;
}

/// <summary>Whether a project is still worked on.</summary>
public enum ProjectStatus
{
    /// <summary>In normal use.</summary>
    Active = 1,

    /// <summary>Out of the way, still reachable from history.</summary>
    Archived = 2,
}
