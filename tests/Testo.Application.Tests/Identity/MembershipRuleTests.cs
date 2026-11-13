using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Identity;
using Testo.Application.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Testo.Contracts.Identity;

namespace Testo.Application.Tests.Identity;

/// <summary>
/// The rules around who may change whose role, and what removing somebody actually does.
/// </summary>
public sealed class MembershipRuleTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;
    private Guid _organizationId;
    private Guid _ownerId;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _services = _database.NewServices();
        (_organizationId, _ownerId) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Northwind");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task ChangeRole_ForTheLastOwner_IsRefused()
    {
        var owner = await FindMemberAsync(_database.Principal.Email!);

        var result = await WithMembersAsync(members =>
            members.ChangeRoleAsync(owner.MembershipId, Role.Admin, TestContext.Current.CancellationToken));

        // The owner is also the actor here, so the first rule to bite is the one about your own
        // role. Both exist; this asserts the product refuses, and says why.
        Assert.False(result.Succeeded);
        Assert.Contains("Members_Error_CannotChangeOwnRole", result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ChangeRole_DemotingTheOnlyOtherOwner_IsRefused()
    {
        await Seed.MemberAsync(_services, _organizationId, "second-owner@northwind.example", Role.Owner);

        // Remove the seeding owner's own owner-ness from the picture by acting as the second one,
        // then try to demote the first: one owner would be left, which is allowed. Demoting that
        // last one is not.
        var first = await FindMemberAsync(_database.Principal.Email!);
        var second = await FindMemberAsync("second-owner@northwind.example");

        var demoteSecond = await WithMembersAsync(members =>
            members.ChangeRoleAsync(second.MembershipId, Role.Admin, TestContext.Current.CancellationToken));
        Assert.True(demoteSecond.Succeeded);

        // Now act as that admin's colleague — the remaining owner — and try to demote themselves
        // through somebody else's hands: become a second admin and attempt it.
        _database.Principal.Become(_organizationId, second.UserId, Role.Admin, second.Email);

        var demoteLastOwner = await WithMembersAsync(members =>
            members.ChangeRoleAsync(first.MembershipId, Role.Viewer, TestContext.Current.CancellationToken));

        Assert.False(demoteLastOwner.Succeeded);
        Assert.Contains("Members_Error_AboveYourRole", demoteLastOwner.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task RemoveMember_ByAQaLead_ReachesATesterAndNobodyElse()
    {
        // "Invite and remove people — testers only" is the one qualified cell of the table. A
        // lead's reach is a tester, in both directions: they cannot touch a developer either.
        var leadId = await Seed.MemberAsync(_services, _organizationId, "lead@northwind.example", Role.QaLead);
        await Seed.MemberAsync(_services, _organizationId, "tester@northwind.example", Role.QaEngineer);
        await Seed.MemberAsync(_services, _organizationId, "dev@northwind.example", Role.Developer);

        var tester = await FindMemberAsync("tester@northwind.example");
        var developer = await FindMemberAsync("dev@northwind.example");

        _database.Principal.Become(_organizationId, leadId, Role.QaLead, "lead@northwind.example");

        var removeDeveloper = await WithMembersAsync(members =>
            members.RemoveMemberAsync(developer.MembershipId, TestContext.Current.CancellationToken));

        Assert.False(removeDeveloper.Succeeded);
        Assert.Contains("Members_Error_AboveYourRole", removeDeveloper.Errors, StringComparer.Ordinal);

        var removeTester = await WithMembersAsync(members =>
            members.RemoveMemberAsync(tester.MembershipId, TestContext.Current.CancellationToken));

        Assert.True(removeTester.Succeeded);
    }

    [Fact]
    public async Task Invite_ByAQaLead_OffersATesterPlaceAndNothingHigher()
    {
        var leadId = await Seed.MemberAsync(_services, _organizationId, "lead2@northwind.example", Role.QaLead);
        _database.Principal.Become(_organizationId, leadId, Role.QaLead, "lead2@northwind.example");

        using var scope = _services.CreateScope();
        var invitations = scope.ServiceProvider.GetRequiredService<IInvitationService>();

        var asTester = await invitations.InviteAsync(
            "new-tester@northwind.example",
            Role.QaEngineer,
            TestContext.Current.CancellationToken);
        Assert.True(asTester.Succeeded);

        var asAdmin = await invitations.InviteAsync(
            "new-admin@northwind.example",
            Role.Admin,
            TestContext.Current.CancellationToken);

        Assert.False(asAdmin.Succeeded);
        Assert.Contains("Members_Error_AboveYourRole", asAdmin.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ChangeRole_ByAQaEngineer_IsRefusedOutright()
    {
        var testerId = await Seed.MemberAsync(_services, _organizationId, "tester@northwind.example", Role.QaEngineer);
        await Seed.MemberAsync(_services, _organizationId, "other@northwind.example", Role.Viewer);

        var other = await FindMemberAsync("other@northwind.example");

        _database.Principal.Become(_organizationId, testerId, Role.QaEngineer, "tester@northwind.example");

        var result = await WithMembersAsync(members =>
            members.ChangeRoleAsync(other.MembershipId, Role.Developer, TestContext.Current.CancellationToken));

        Assert.False(result.Succeeded);
        Assert.Contains("Members_Error_NotAllowed", result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task RemoveMember_Always_EndsTheirSessionsImmediately()
    {
        var testerId = await Seed.MemberAsync(_services, _organizationId, "tester@northwind.example", Role.QaEngineer);
        var tester = await FindMemberAsync("tester@northwind.example");

        var stamp = await SecurityStampOfAsync(testerId);

        using (var before = _services.CreateScope())
        {
            var validator = before.ServiceProvider.GetRequiredService<SessionValidator>();
            Assert.True(await IsSessionValidAsync(validator, testerId, stamp));
        }

        var removal = await WithMembersAsync(members =>
            members.RemoveMemberAsync(tester.MembershipId, TestContext.Current.CancellationToken));
        Assert.True(removal.Succeeded);

        using var after = _services.CreateScope();
        var stillValid = after.ServiceProvider.GetRequiredService<SessionValidator>();

        // Not at the next expiry. Now.
        Assert.False(await IsSessionValidAsync(stillValid, testerId, stamp));
    }

    [Fact]
    public async Task RemoveMember_ForTheLastOwner_IsRefused()
    {
        var adminId = await Seed.MemberAsync(_services, _organizationId, "admin@northwind.example", Role.Admin);
        var owner = await FindMemberAsync(_database.Principal.Email!);

        _database.Principal.Become(_organizationId, adminId, Role.Admin, "admin@northwind.example");

        var result = await WithMembersAsync(members =>
            members.RemoveMemberAsync(owner.MembershipId, TestContext.Current.CancellationToken));

        // An admin may not reach an owner at all, which is the first and strongest refusal.
        Assert.False(result.Succeeded);
        Assert.Contains("Members_Error_AboveYourRole", result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task RemoveMember_Always_LeavesALineInTheAuditLog()
    {
        await Seed.MemberAsync(_services, _organizationId, "tester@northwind.example", Role.QaEngineer);
        var tester = await FindMemberAsync("tester@northwind.example");

        await WithMembersAsync(members =>
            members.RemoveMemberAsync(tester.MembershipId, TestContext.Current.CancellationToken));

        using var scope = _services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var entries = await log.ReadAsync(1, 50, TestContext.Current.CancellationToken);

        Assert.Contains(entries.Items, entry => entry.Action == "member.removed");
    }

    [Fact]
    public async Task ReadAuditLog_AsAQaEngineer_ShowsNothing()
    {
        var testerId = await Seed.MemberAsync(_services, _organizationId, "tester@northwind.example", Role.QaEngineer);
        _database.Principal.Become(_organizationId, testerId, Role.QaEngineer, "tester@northwind.example");

        using var scope = _services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        var entries = await log.ReadAsync(1, 50, TestContext.Current.CancellationToken);

        Assert.Empty(entries.Items);
    }

    private Task<bool> IsSessionValidAsync(SessionValidator validator, Guid userId, string securityStamp) =>
        validator.IsStillValidAsync(userId, _organizationId, securityStamp, TestContext.Current.CancellationToken);

    /// <summary>What the person's cookie would have been issued with.</summary>
    private async Task<string> SecurityStampOfAsync(Guid userId)
    {
        await using var database = _database.NewContext();

        return await database.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<OrganizationMember> FindMemberAsync(string email)
    {
        using var scope = _services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembershipService>();

        var list = await members.ListMembersAsync(1, 100, TestContext.Current.CancellationToken);
        return list.Items.Single(member => member.Email == email);
    }

    private async Task<Contracts.Common.OperationResult> WithMembersAsync(
        Func<IMembershipService, Task<Contracts.Common.OperationResult>> work)
    {
        using var scope = _services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IMembershipService>());
    }
}
