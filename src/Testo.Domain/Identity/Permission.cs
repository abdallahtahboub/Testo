using System.Diagnostics.CodeAnalysis;

namespace Testo.Domain.Identity;

/// <summary>
/// One row of the permission table in <c>docs/identity-and-access.md</c> §2.
/// </summary>
/// <remarks>
/// Permissions are named for the action a person takes, not for the screen it sits on, so that
/// moving a button between pages does not change who may press it.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "CA1711 reserves the Permission suffix for Code Access Security types, which .NET no longer has. "
                  + "This is the permission of docs/identity-and-access.md \u00a72, and any other name would read worse.")]
public enum Permission
{
    /// <summary>Read projects, tests, runs and defects.</summary>
    Read = 1,

    /// <summary>Create and edit test cases.</summary>
    ManageTestCases = 2,

    /// <summary>Start, stop and rerun tests.</summary>
    RunTests = 3,

    /// <summary>Report a defect.</summary>
    ReportDefect = 4,

    /// <summary>Triage a defect: severity, priority, assignee.</summary>
    TriageDefect = 5,

    /// <summary>Change a defect assigned to them.</summary>
    ChangeOwnDefect = 6,

    /// <summary>Approve test cases and plans.</summary>
    ApproveTestArtifacts = 7,

    /// <summary>Record a release decision.</summary>
    RecordReleaseDecision = 8,

    /// <summary>Create and archive projects.</summary>
    ManageProjects = 9,

    /// <summary>Invite and remove people, and set their roles.</summary>
    ManagePeople = 10,

    /// <summary>Integrations, single sign-on and organization settings.</summary>
    ManageOrganization = 11,

    /// <summary>Read and export the audit log.</summary>
    ReadAuditLog = 12,

    /// <summary>Billing, and deleting the organization.</summary>
    ManageBilling = 13,
}
