namespace Testo.Domain.Identity;

/// <summary>
/// The permission table of <c>docs/identity-and-access.md</c> §2, expressed once.
/// </summary>
/// <remarks>
/// <para>
/// Every check in the product comes through here, so there is one answer to "may this person do
/// this" rather than one per screen. <c>PermissionMatrixTests</c> transcribes the table from the
/// document independently and asserts every cell, so the two cannot drift apart.
/// </para>
/// <para>
/// This says what a <em>role</em> may do. Whether the specific object is in reach — the person's
/// own defect, the last owner of an organization — is a separate question, answered by the methods
/// below and by the use cases in <c>Testo.Application</c>.
/// </para>
/// </remarks>
public static class Permissions
{
    private static readonly Dictionary<Role, HashSet<Permission>> Granted = new()
    {
        [Role.Owner] =
        [
            Permission.Read,
            Permission.ManageTestCases,
            Permission.RunTests,
            Permission.ReportDefect,
            Permission.TriageDefect,
            Permission.ChangeOwnDefect,
            Permission.ApproveTestArtifacts,
            Permission.RecordReleaseDecision,
            Permission.ManageProjects,
            Permission.ManagePeople,
            Permission.ManageOrganization,
            Permission.ReadAuditLog,
            Permission.ManageBilling,
        ],
        [Role.Admin] =
        [
            Permission.Read,
            Permission.ReportDefect,
            Permission.ChangeOwnDefect,
            Permission.ManageProjects,
            Permission.ManagePeople,
            Permission.ManageOrganization,
            Permission.ReadAuditLog,
        ],
        [Role.QaLead] =
        [
            Permission.Read,
            Permission.ManageTestCases,
            Permission.RunTests,
            Permission.ReportDefect,
            Permission.TriageDefect,
            Permission.ChangeOwnDefect,
            Permission.ApproveTestArtifacts,
            Permission.RecordReleaseDecision,
            Permission.ManageProjects,
            Permission.ManagePeople,
        ],
        [Role.QaEngineer] =
        [
            Permission.Read,
            Permission.ManageTestCases,
            Permission.RunTests,
            Permission.ReportDefect,
            Permission.TriageDefect,
            Permission.ChangeOwnDefect,
        ],
        [Role.Developer] =
        [
            Permission.Read,
            Permission.ReportDefect,
            Permission.ChangeOwnDefect,
        ],
        [Role.Viewer] =
        [
            Permission.Read,
        ],
    };

    /// <summary>Whether a role carries a permission at all.</summary>
    public static bool Allows(Role role, Permission permission) =>
        Granted.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    /// <summary>Every permission a role carries, for building a claims principal once per session.</summary>
    public static IReadOnlySet<Permission> Of(Role role) =>
        Granted.TryGetValue(role, out var permissions) ? permissions : new HashSet<Permission>();

    /// <summary>
    /// Whether <paramref name="actor"/> may give somebody the role <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    /// Nobody raises anyone above their own standing, which is why this is not simply
    /// <see cref="Permission.ManagePeople"/>. A QA lead may invite testers and no one else — the
    /// "testers only" cell of the table.
    /// </remarks>
    public static bool MayAssignRole(Role actor, Role target) => actor switch
    {
        Role.Owner => true,
        Role.Admin => target != Role.Owner,
        Role.QaLead => target == Role.QaEngineer,
        _ => false,
    };
}
