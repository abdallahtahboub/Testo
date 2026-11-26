using Microsoft.AspNetCore.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Identity;
using Autestiq.Modules.Settings.Pages;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Settings;

/// <summary>
/// The members page (E1.1). The services decide every change; these check that the page shows what they say, asks before
/// removing someone, says a refusal in words, and offers nothing to a person who may not manage people.
/// </summary>
public class MembersPageTests : DesignSystemTestContext
{
    private static readonly Guid Me = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeMemberships _members = new();
    private readonly FakeInvitations _invitations = new();
    private readonly Person _person = new(Role.Owner);

    public MembersPageTests()
    {
        Services.AddSingleton<IMembershipService>(_members);
        Services.AddSingleton<IInvitationService>(_invitations);
        Services.AddSingleton<ICurrentUser>(_person);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    [Fact]
    public void Render_AsAnOwner_ListsMembers_WithTheirRoles_AndMarksYou()
    {
        var page = Render<MembersPage>();

        Assert.Equal("Members", page.Find("h1").TextContent.Trim());
        Assert.Contains("(you)", page.Find("[data-testid='member-mara@northwind.example']").TextContent);
        Assert.Contains("Sven Albrecht", page.Find("[data-testid='member-sven@northwind.example']").TextContent);

        // Your own role is shown, not offered for change, and you are not offered your own removal.
        Assert.Empty(page.FindAll("[data-testid='role-mara@northwind.example']"));
        Assert.Empty(page.FindAll("[data-testid='remove-mara@northwind.example']"));
        Assert.NotNull(page.Find("[data-testid='role-sven@northwind.example']"));
    }

    [Fact]
    public void Render_AsAViewer_OffersNothing_AndAsksTheServicesForNothing()
    {
        _person.Role = Role.Viewer;

        var page = Render<MembersPage>();

        Assert.NotNull(page.Find("[data-testid='members-not-allowed']"));
        Assert.Empty(page.FindAll("[data-testid='invite']"));
        Assert.Equal(0, _members.Listed);
    }

    [Fact]
    public void Remove_AsksFirst_AndOnlyThenRemoves()
    {
        var page = Render<MembersPage>();

        page.Find("[data-testid='remove-sven@northwind.example']").Click();

        Assert.Contains("Remove Sven Albrecht from this organization?", page.Find("[data-testid='remove-confirm']").TextContent);
        Assert.Empty(_members.Removed);

        page.Find("[data-testid='remove-confirm'] .t-button--danger").Click();

        Assert.Equal([_members.Sven.MembershipId], _members.Removed);
        Assert.Contains("Sven Albrecht was removed.", page.Find("[data-testid='members-done']").TextContent);
    }

    [Fact]
    public void ARefusal_IsSaidInWords()
    {
        _members.Refusal = "Members_Error_LastOwner";
        var page = Render<MembersPage>();

        page.Find("[data-testid='role-sven@northwind.example']").Change("Viewer");

        Assert.Equal("The organization must keep at least one owner.", page.Find("[data-testid='members-problem']").TextContent.Trim());
    }

    [Fact]
    public void Invite_SendsTheAddressAndRole_AndResendInvitesAgain()
    {
        var page = Render<MembersPage>();

        page.Find("input[data-testid='invite-email']").Input("lena@northwind.example");
        page.Find("[data-testid='invite-role']").Change("QaLead");
        page.Find("[data-testid='invite']").Click();

        Assert.Contains(("lena@northwind.example", Role.QaLead), _invitations.Sent);

        page.Find("[data-testid='resend-anna@northwind.example']").Click();

        Assert.Contains(("anna@northwind.example", Role.Developer), _invitations.Sent);
        Assert.Contains("the earlier link no longer works", page.Find("[data-testid='members-done']").TextContent);
    }

    [Fact]
    public void Render_InGerman_UsesTheGermanTexts()
    {
        using var german = new CultureScope("de");

        var page = Render<MembersPage>();

        Assert.Equal("Mitglieder", page.Find("h1").TextContent.Trim());
        Assert.Contains("Offene Einladungen", page.Markup);
    }

    private sealed class Person(Role role) : ICurrentUser
    {
        public Role? Role { get; set; } = role;

        public bool IsSignedIn => true;

        public Guid? UserId => Me;

        public string? Email => "mara@northwind.example";

        public string? DisplayName => "Mara Lindqvist";

        public bool Can(Permission permission) =>
            permission == Permission.ManagePeople ? Role is Contracts.Identity.Role.Owner or Contracts.Identity.Role.Admin : permission == Permission.Read;
    }

    private sealed class FakeMemberships : IMembershipService
    {
        public OrganizationMember Sven { get; } = new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Sven Albrecht", "sven@northwind.example", Role.Admin, DateTimeOffset.UnixEpoch, true, false);

        public int Listed { get; private set; }

        public List<Guid> Removed { get; } = [];

        public string? Refusal { get; set; }

        public Task<PagedResult<OrganizationMember>> ListMembersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            Listed++;
            OrganizationMember[] members =
            [
                new(Guid.CreateVersion7(), Me, "Mara Lindqvist", "mara@northwind.example", Role.Owner, DateTimeOffset.UnixEpoch, true, true),
                Sven,
            ];

            return Task.FromResult(new PagedResult<OrganizationMember>([.. members.Where(member => !Removed.Contains(member.MembershipId))], 2, page, pageSize));
        }

        public Task<IReadOnlyList<OrganizationSummary>> ListMyOrganizationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OrganizationSummary>>([]);

        public Task<OperationResult> ChangeRoleAsync(Guid membershipId, Role newRole, CancellationToken cancellationToken = default) =>
            Task.FromResult(Refusal is { } refusal ? OperationResult.Failure(refusal) : OperationResult.Success);

        public Task<OperationResult> RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default)
        {
            Removed.Add(membershipId);
            return Task.FromResult(OperationResult.Success);
        }
    }

    private sealed class FakeInvitations : IInvitationService
    {
        public List<(string Email, Role Role)> Sent { get; } = [];

        public Task<PagedResult<PendingInvitation>> ListPendingAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<PendingInvitation>(
                [new PendingInvitation(Guid.CreateVersion7(), "anna@northwind.example", Role.Developer, "Mara Lindqvist", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(7))],
                1,
                page,
                pageSize));

        public Task<OperationResult> InviteAsync(string email, Role role, CancellationToken cancellationToken = default)
        {
            Sent.Add((email, role));
            return Task.FromResult(OperationResult.Success);
        }

        public Task<OperationResult> RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default) => Task.FromResult(OperationResult.Success);

        public Task<InvitationDetails?> DescribeAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult> AcceptAsync(string token, string? displayName, string? password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
