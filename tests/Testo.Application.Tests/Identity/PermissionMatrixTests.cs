using Testo.Contracts.Identity;
using Testo.Domain.Identity;

namespace Testo.Application.Tests.Identity;

/// <summary>
/// Walks the permission table of <c>docs/identity-and-access.md</c> §2 and asserts every cell.
/// </summary>
/// <remarks>
/// The table below is transcribed from the document, independently of
/// <see cref="Permissions"/>. That is the whole point: if somebody changes the code without
/// changing the document, or the document without the code, these tests fail and say which cell
/// disagrees. A test that read the table out of the code it is testing would prove nothing.
/// </remarks>
public class PermissionMatrixTests
{
    /// <summary>The roles, in the column order the document uses.</summary>
    private static readonly Role[] Columns =
        [Role.Owner, Role.Admin, Role.QaLead, Role.QaEngineer, Role.Developer, Role.Viewer];

    /// <summary>
    /// One row of the document's table: the permission, then a dot for each role that has it, in
    /// the column order above.
    /// </summary>
    private static readonly Dictionary<Permission, bool[]> Documented = new()
        {
            //                                        Owner  Admin  Lead   Eng    Dev    Viewer
            { Permission.Read,                       [true,  true,  true,  true,  true,  true] },
            { Permission.ManageTestCases,            [true,  false, true,  true,  false, false] },
            { Permission.RunTests,                   [true,  false, true,  true,  false, false] },
            { Permission.ReportDefect,               [true,  true,  true,  true,  true,  false] },
            { Permission.TriageDefect,               [true,  false, true,  true,  false, false] },
            { Permission.ChangeOwnDefect,            [true,  true,  true,  true,  true,  false] },
            { Permission.ApproveTestArtifacts,       [true,  false, true,  false, false, false] },
            { Permission.RecordReleaseDecision,      [true,  false, true,  false, false, false] },
            { Permission.ManageProjects,             [true,  true,  true,  false, false, false] },
            { Permission.ManagePeople,               [true,  true,  true,  false, false, false] },
            { Permission.ManageOrganization,         [true,  true,  false, false, false, false] },
            { Permission.ReadAuditLog,               [true,  true,  false, false, false, false] },
            { Permission.ManageBilling,              [true,  false, false, false, false, false] },
        };

    /// <summary>The same table, in the shape xUnit wants for a theory.</summary>
    public static TheoryData<Permission, bool[]> DocumentedTable
    {
        get
        {
            var data = new TheoryData<Permission, bool[]>();

            foreach (var (permission, allowed) in Documented)
            {
                data.Add(permission, allowed);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DocumentedTable))]
    public void Allows_ForEveryRole_MatchesTheDocumentedTable(Permission permission, bool[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        for (var column = 0; column < Columns.Length; column++)
        {
            var role = Columns[column];

            Assert.True(
                Permissions.Allows(role, permission) == expected[column],
                $"docs/identity-and-access.md §2 says {role} {(expected[column] ? "may" : "may not")} "
                + $"{permission}, and the code disagrees.");
        }
    }

    [Fact]
    public void DocumentedTable_Always_CoversEveryPermission()
    {
        // A permission added to the code without a row here would otherwise go unchecked.
        Assert.Equal(Enum.GetValues<Permission>().ToHashSet(), Documented.Keys.ToHashSet());
    }

    [Fact]
    public void DocumentedTable_Always_CoversEveryRole() =>
        Assert.Equal(Enum.GetValues<Role>().ToHashSet(), Columns.ToHashSet());

    [Theory]
    [InlineData(Role.Owner, Role.Owner, true)]
    [InlineData(Role.Owner, Role.Viewer, true)]
    [InlineData(Role.Admin, Role.Admin, true)]
    [InlineData(Role.Admin, Role.QaEngineer, true)]
    [InlineData(Role.QaLead, Role.QaEngineer, true)]
    public void MayAssignRole_WithinReach_IsAllowed(Role actor, Role target, bool expected) =>
        Assert.Equal(expected, Permissions.MayAssignRole(actor, target));

    [Theory]
    [InlineData(Role.Admin, Role.Owner)]
    [InlineData(Role.QaLead, Role.Owner)]
    [InlineData(Role.QaLead, Role.Admin)]
    [InlineData(Role.QaLead, Role.QaLead)]
    [InlineData(Role.QaLead, Role.Viewer)]
    [InlineData(Role.QaEngineer, Role.Viewer)]
    [InlineData(Role.Developer, Role.Viewer)]
    [InlineData(Role.Viewer, Role.Viewer)]
    public void MayAssignRole_AboveTheirOwnStanding_IsRefused(Role actor, Role target) =>
        Assert.False(
            Permissions.MayAssignRole(actor, target),
            $"{actor} must not be able to make somebody {target}.");

    [Fact]
    public void MayAssignRole_ForAQaLead_IsTestersOnly()
    {
        // The one qualified cell in the table: "Invite and remove people — testers only".
        var assignable = Enum.GetValues<Role>().Where(role => Permissions.MayAssignRole(Role.QaLead, role));

        Assert.Equal([Role.QaEngineer], assignable);
    }
}
