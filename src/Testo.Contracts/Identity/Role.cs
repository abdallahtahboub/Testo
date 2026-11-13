namespace Testo.Contracts.Identity;

/// <summary>
/// What a person may do in one organization (<c>docs/identity-and-access.md</c> §2).
/// A person has exactly one role per organization, and may hold a different one elsewhere.
/// </summary>
/// <remarks>
/// Six roles: fewer would not express a QA team, more would not be understood. The numbers are
/// stored, so they are assigned explicitly and never reordered.
/// </remarks>
public enum Role
{
    /// <summary>Read only. A stakeholder who watches but does not act.</summary>
    Viewer = 1,

    /// <summary>Reads everything, comments, and changes the defects assigned to them.</summary>
    Developer = 2,

    /// <summary>Creates and runs tests, reports and triages defects. Cannot approve a release.</summary>
    QaEngineer = 3,

    /// <summary>Everything in testing: plans, approvals, release decisions. May invite testers.</summary>
    QaLead = 4,

    /// <summary>Manages people, roles, integrations, single sign-on and settings. Not billing.</summary>
    Admin = 5,

    /// <summary>Everything, including billing and deleting the organization. At least one always exists.</summary>
    Owner = 6,
}
