using Testo.Domain.Common;

namespace Testo.Domain.Identity;

/// <summary>The customer company. The tenant boundary everything else sits inside.</summary>
public sealed class Organization : Entity
{
    /// <summary>The company name as it is shown.</summary>
    public required string Name { get; set; }

    /// <summary>A short lower-case name used in URLs. Unique across the product.</summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Where the data physically sits, chosen when the organization is created and never changed.
    /// German customers ask this before they ask anything else.
    /// </summary>
    public required string DataRegion { get; set; }

    /// <summary>A placeholder until billing exists (<c>docs/domain-model.md</c> §6).</summary>
    public string Plan { get; set; } = "trial";

    /// <summary>Whether the organization may be used.</summary>
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;

    /// <summary>
    /// Whether every member must have a second factor. Optional per person, enforceable per
    /// organization (<c>docs/identity-and-access.md</c> §1).
    /// </summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>The people in this organization.</summary>
    public ICollection<Membership> Memberships { get; } = [];
}

/// <summary>Whether an organization may be used.</summary>
public enum OrganizationStatus
{
    /// <summary>In normal use.</summary>
    Active = 1,

    /// <summary>Sign-in refused, data kept.</summary>
    Suspended = 2,
}
