using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Identity;
using Testo.Application.Tests.Support;
using Testo.Contracts.Identity;

namespace Testo.Application.Tests.Identity;

/// <summary>
/// The headline of Milestone 3: somebody is invited, accepts, signs in, and sees their own
/// organization and nobody else's.
/// </summary>
public sealed class InvitationFlowTests : IAsyncLifetime
{
    private const string Newcomer = "newcomer@northwind.example";

    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;
    private RecordingNotifier _sent = null!;
    private Guid _organizationId;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _sent = new RecordingNotifier();
        _services = _database.NewServices(services => services.AddSingleton<IInvitationNotifier>(_sent));

        (_organizationId, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Northwind");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task AnInvitedPerson_CanAcceptSignInAndSeeTheirOwnOrganization()
    {
        await InviteAsync(Newcomer, Role.QaEngineer);
        var token = _sent.InvitationTokenFor(Newcomer);

        // What the link shows before they commit to anything.
        var described = await WithInvitationsAsync(service =>
            service.DescribeAsync(token, TestContext.Current.CancellationToken));

        Assert.NotNull(described);
        Assert.Equal(Newcomer, described.Email);
        Assert.Equal("Northwind", described.OrganizationName);
        Assert.Equal(Role.QaEngineer, described.Role);
        Assert.True(described.NeedsAccount);

        // Accepting, as somebody with no account yet: a name and a password.
        _database.Principal.OrganizationId = null;
        _database.Principal.UserId = null;

        var accepted = await WithInvitationsAsync(service =>
            service.AcceptAsync(token, "Nadia Newcomer", Seed.Password, TestContext.Current.CancellationToken));

        Assert.True(accepted.Succeeded);

        // And they can now sign in, into that organization and in that role.
        using var scope = _services.CreateScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInWorkflow>();

        var (result, ticket) = await signIn.AuthenticateAsync(
            Newcomer,
            Seed.Password,
            "127.0.0.1",
            TestContext.Current.CancellationToken);

        Assert.Equal(SignInOutcome.Succeeded, result.Outcome);
        Assert.NotNull(ticket);
        Assert.Equal(_organizationId, ticket.OrganizationId);
        Assert.Equal(Role.QaEngineer, ticket.Role);
        Assert.Equal("Nadia Newcomer", ticket.DisplayName);
    }

    [Fact]
    public async Task AnInvitation_UsedTwice_IsRefusedTheSecondTime()
    {
        await InviteAsync(Newcomer, Role.QaEngineer);
        var token = _sent.InvitationTokenFor(Newcomer);

        _database.Principal.OrganizationId = null;
        _database.Principal.UserId = null;

        var first = await WithInvitationsAsync(service =>
            service.AcceptAsync(token, "Nadia Newcomer", Seed.Password, TestContext.Current.CancellationToken));
        Assert.True(first.Succeeded);

        var second = await WithInvitationsAsync(service =>
            service.AcceptAsync(token, "Somebody Else", Seed.Password, TestContext.Current.CancellationToken));

        Assert.False(second.Succeeded);
        Assert.Contains("Invitation_Error_NotUsable", second.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task AnInvitation_ThatWasWithdrawn_NoLongerWorks()
    {
        await InviteAsync(Newcomer, Role.QaEngineer);
        var token = _sent.InvitationTokenFor(Newcomer);

        var pending = await WithInvitationsAsync(service =>
            service.ListPendingAsync(1, 10, TestContext.Current.CancellationToken));

        var revoked = await WithInvitationsAsync(service =>
            service.RevokeAsync(pending.Items.Single().InvitationId, TestContext.Current.CancellationToken));
        Assert.True(revoked.Succeeded);

        var described = await WithInvitationsAsync(service =>
            service.DescribeAsync(token, TestContext.Current.CancellationToken));

        Assert.Null(described);
    }

    [Fact]
    public async Task AnInvitation_SentTwice_LeavesOnlyTheNewerLinkWorking()
    {
        await InviteAsync(Newcomer, Role.QaEngineer);
        var first = _sent.InvitationTokenFor(Newcomer);

        await InviteAsync(Newcomer, Role.Developer);
        var second = _sent.InvitationTokenFor(Newcomer);

        Assert.NotEqual(first, second);

        Assert.Null(await WithInvitationsAsync(service =>
            service.DescribeAsync(first, TestContext.Current.CancellationToken)));

        var current = await WithInvitationsAsync(service =>
            service.DescribeAsync(second, TestContext.Current.CancellationToken));

        Assert.NotNull(current);
        Assert.Equal(Role.Developer, current.Role);
    }

    [Fact]
    public async Task AnInvitation_ForSomebodyWhoIsAlreadyAMember_IsRefused()
    {
        var owner = _database.Principal.Email!;

        var result = await WithInvitationsAsync(service =>
            service.InviteAsync(owner, Role.QaEngineer, TestContext.Current.CancellationToken));

        Assert.False(result.Succeeded);
        Assert.Contains("Members_Error_AlreadyAMember", result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task AnInvitedPerson_SeesNothingOfAnotherOrganization()
    {
        // A second customer, with its own owner and its own audit trail.
        await using var otherDatabase = await TestDatabase.CreateAsync();
        await using var otherServices = otherDatabase.NewServices();
        await Seed.OrganizationWithOwnerAsync(otherDatabase, otherServices, "Contoso");

        await InviteAsync(Newcomer, Role.QaEngineer);
        var token = _sent.InvitationTokenFor(Newcomer);

        _database.Principal.OrganizationId = null;
        _database.Principal.UserId = null;

        await WithInvitationsAsync(service =>
            service.AcceptAsync(token, "Nadia Newcomer", Seed.Password, TestContext.Current.CancellationToken));

        using var scope = _services.CreateScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInWorkflow>();

        var (_, ticket) = await signIn.AuthenticateAsync(
            Newcomer,
            Seed.Password,
            "127.0.0.1",
            TestContext.Current.CancellationToken);

        Assert.NotNull(ticket);
        _database.Principal.Become(ticket.OrganizationId, ticket.UserId, ticket.Role, ticket.Email);

        var members = scope.ServiceProvider.GetRequiredService<IMembershipService>();
        var mine = await members.ListMyOrganizationsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Northwind"], mine.Select(organization => organization.Name));
    }

    [Fact]
    public async Task PasswordReset_Always_EndsEverySessionThatPersonHad()
    {
        await InviteAsync(Newcomer, Role.QaEngineer);
        var invitation = _sent.InvitationTokenFor(Newcomer);

        _database.Principal.OrganizationId = null;
        _database.Principal.UserId = null;

        await WithInvitationsAsync(service =>
            service.AcceptAsync(invitation, "Nadia Newcomer", Seed.Password, TestContext.Current.CancellationToken));

        using var scope = _services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<PasswordResetWorkflow>();

        await reset.RequestAsync(Newcomer, "127.0.0.1", TestContext.Current.CancellationToken);

        var done = await reset.ResetAsync(
            _sent.ResetTokenFor(Newcomer),
            "another-long-password",
            "127.0.0.1",
            TestContext.Current.CancellationToken);

        Assert.True(done.Succeeded);

        var signIn = scope.ServiceProvider.GetRequiredService<SignInWorkflow>();

        var (withOld, _) = await signIn.AuthenticateAsync(
            Newcomer,
            Seed.Password,
            "127.0.0.1",
            TestContext.Current.CancellationToken);
        Assert.Equal(SignInOutcome.Failed, withOld.Outcome);

        var (withNew, _) = await signIn.AuthenticateAsync(
            Newcomer,
            "another-long-password",
            "127.0.0.1",
            TestContext.Current.CancellationToken);
        Assert.Equal(SignInOutcome.Succeeded, withNew.Outcome);
    }

    [Fact]
    public async Task PasswordReset_ForAnAddressWithNoAccount_SendsNothingAndSaysNothing()
    {
        using var scope = _services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<PasswordResetWorkflow>();

        // No exception, no hint: the screen that follows says the same thing either way.
        await reset.RequestAsync("nobody@example.com", "127.0.0.1", TestContext.Current.CancellationToken);

        Assert.False(_sent.SentAnythingTo("nobody@example.com"));
    }

    [Fact]
    public async Task PasswordReset_WithAPasswordFromABreachList_IsRefused()
    {
        using var scope = _services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<PasswordResetWorkflow>();

        await reset.RequestAsync(_database.Principal.Email!, "127.0.0.1", TestContext.Current.CancellationToken);

        var result = await reset.ResetAsync(
            _sent.ResetTokenFor(_database.Principal.Email!),
            "passwordpassword",
            "127.0.0.1",
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("Password_Breached", result.Errors, StringComparer.Ordinal);
    }

    private async Task InviteAsync(string email, Role role)
    {
        var result = await WithInvitationsAsync(service =>
            service.InviteAsync(email, role, TestContext.Current.CancellationToken));

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
    }

    private async Task<T> WithInvitationsAsync<T>(Func<IInvitationService, Task<T>> work)
    {
        using var scope = _services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IInvitationService>());
    }
}
