using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Identity;
using Testo.Application.Tests.Support;
using Testo.Contracts.Identity;

namespace Testo.Application.Tests.Identity;

/// <summary>What happens at the sign-in screen, including everything that goes wrong there.</summary>
public sealed class SignInTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;
    private Guid _organizationId;
    private string _ownerEmail = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _services = _database.NewServices();

        (_organizationId, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Northwind");
        _ownerEmail = _database.Principal.Email!;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task SignIn_WithTheRightPassword_Succeeds()
    {
        var (result, ticket) = await AuthenticateAsync(_ownerEmail, Seed.Password);

        Assert.Equal(SignInOutcome.Succeeded, result.Outcome);
        Assert.NotNull(ticket);
        Assert.Equal(_organizationId, ticket.OrganizationId);
        Assert.Equal(Role.Owner, ticket.Role);
    }

    [Fact]
    public async Task SignIn_WithADifferentCase_StillFindsTheAccount()
    {
        var (result, _) = await AuthenticateAsync(_ownerEmail.ToUpperInvariant(), Seed.Password);

        Assert.Equal(SignInOutcome.Succeeded, result.Outcome);
    }

    [Fact]
    public async Task SignIn_WithTheWrongPassword_Fails()
    {
        var (result, ticket) = await AuthenticateAsync(_ownerEmail, "not the right password");

        Assert.Equal(SignInOutcome.Failed, result.Outcome);
        Assert.Null(ticket);
    }

    [Fact]
    public async Task SignIn_WithAnAddressThatHasNoAccount_FailsTheSameWay()
    {
        // Identical to the wrong-password case, deliberately: the screen must not say which of
        // the two it was (docs/identity-and-access.md §5).
        var (result, _) = await AuthenticateAsync("nobody@example.com", Seed.Password);

        Assert.Equal(SignInOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task SignIn_AfterTenWrongPasswords_LocksTheAccountAndSaysSo()
    {
        for (var attempt = 0; attempt < 9; attempt++)
        {
            var (refused, _) = await AuthenticateAsync(_ownerEmail, "wrong");
            Assert.Equal(SignInOutcome.Failed, refused.Outcome);
        }

        var (locked, _) = await AuthenticateAsync(_ownerEmail, "wrong");

        Assert.Equal(SignInOutcome.LockedOut, locked.Outcome);
        Assert.NotNull(locked.LockoutEnds);

        // And the right password does not get past a lock either.
        var (stillLocked, _) = await AuthenticateAsync(_ownerEmail, Seed.Password);
        Assert.Equal(SignInOutcome.LockedOut, stillLocked.Outcome);
    }

    [Fact]
    public async Task SignIn_ForSomebodyWhoBelongsNowhere_SaysSoRatherThanLettingThemIn()
    {
        await Seed.MemberAsync(_services, _organizationId, "leaver@northwind.example", Role.QaEngineer);

        using (var scope = _services.CreateScope())
        {
            var members = scope.ServiceProvider.GetRequiredService<IMembershipService>();
            var list = await members.ListMembersAsync(1, 50, TestContext.Current.CancellationToken);
            var leaver = list.Items.Single(member => member.Email == "leaver@northwind.example");

            var removal = await members.RemoveMemberAsync(leaver.MembershipId, TestContext.Current.CancellationToken);
            Assert.True(removal.Succeeded);
        }

        var (result, ticket) = await AuthenticateAsync("leaver@northwind.example", Seed.Password);

        Assert.Equal(SignInOutcome.NoActiveMembership, result.Outcome);
        Assert.Null(ticket);
    }

    [Fact]
    public async Task SignIn_WhereTheOrganizationRequiresASecondFactor_SendsThemToSetOneUp()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var services = database.NewServices();

        await Seed.OrganizationWithOwnerAsync(database, services, "Strict", requiresTwoFactor: true);

        using var scope = services.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<SignInWorkflow>();

        var (result, ticket) = await workflow.AuthenticateAsync(
            database.Principal.Email!,
            Seed.Password,
            "127.0.0.1",
            TestContext.Current.CancellationToken);

        Assert.Equal(SignInOutcome.RequiresTwoFactorSetup, result.Outcome);
        Assert.Null(ticket);
    }

    [Fact]
    public async Task SignIn_Always_LeavesALineInTheAuditLog()
    {
        await AuthenticateAsync(_ownerEmail, Seed.Password);

        using var scope = _services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        var entries = await log.ReadAsync(1, 50, TestContext.Current.CancellationToken);

        Assert.Contains(entries.Items, entry => entry.Action == "user.signed_in" && entry.ActorName == _ownerEmail);
    }

    private async Task<(SignInResult Result, SignInTicket? Ticket)> AuthenticateAsync(string email, string password)
    {
        using var scope = _services.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<SignInWorkflow>();

        return await workflow.AuthenticateAsync(email, password, "127.0.0.1", TestContext.Current.CancellationToken);
    }
}
