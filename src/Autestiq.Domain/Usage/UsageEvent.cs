using Autestiq.Contracts.Usage;
using Autestiq.Domain.Common;

namespace Autestiq.Domain.Usage;

/// <summary>
/// One measured use of the product, for billing and for quotas (Milestone 3.6). Append-only, and recorded once:
/// the idempotency key is unique within the organization, so recording the same thing twice stores it once.
/// </summary>
public sealed class UsageEvent : Entity, IBelongsToOrganization, IAppendOnly
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The project it was for, when there is one.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The run it was for, when there is one.</summary>
    public Guid? RunId { get; set; }

    /// <summary>What is measured; the unit follows from it.</summary>
    public UsageCategory Category { get; set; }

    /// <summary>How much, in the category's unit.</summary>
    public long Quantity { get; set; }

    /// <summary>Whether it counts towards the bill and the quota (decision B, commercial readiness §9).</summary>
    public bool Billable { get; set; }

    /// <summary>The month it belongs to, <c>yyyy-MM</c> in UTC.</summary>
    public required string Period { get; set; }

    /// <summary>When it happened.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>What makes it one event however often it is recorded, such as the run and the category.</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>What recorded it.</summary>
    public required string Source { get; set; }
}
