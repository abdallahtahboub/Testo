using Microsoft.Extensions.DependencyInjection;
using Testo.Contracts.Identity;
using Testo.Domain.Identity;
using Testo.Persistence;
using Testo.Persistence.Identity;

namespace Testo.Application.Tests.Support;

/// <summary>Puts a believable organization into a test database.</summary>
public static class Seed
{
    /// <summary>The password every seeded person has, long enough to pass the rules.</summary>
    public const string Password = "a-long-enough-password";

    /// <summary>
    /// Creates an organization with one owner, and leaves the test signed in as that owner.
    /// </summary>
    public static async Task<(Guid OrganizationId, Guid OwnerId)> OrganizationWithOwnerAsync(
        TestDatabase database,
        IServiceProvider services,
        string name,
        bool requiresTwoFactor = false)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(services);

        database.Principal.OrganizationId = null;
        database.Principal.UserId = null;

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestoDbContext>();
        var credentials = scope.ServiceProvider.GetRequiredService<CredentialService>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        var organization = new Organization
        {
            Name = name,
            Slug = name.ToLowerInvariant(),
            DataRegion = "eu-central",
            RequiresTwoFactor = requiresTwoFactor,
        };

        var owner = await PersonAsync(credentials, $"owner@{name.ToLowerInvariant()}.example", $"{name} Owner");

        context.Organizations.Add(organization);
        context.Users.Add(owner);
        context.Memberships.Add(new Membership
        {
            OrganizationId = organization.Id,
            UserId = owner.Id,
            Role = Role.Owner,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        // Creating an organization happens outside any organization, as it does in production.
        using (writeScope.AllowWritingTo(organization.Id))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        database.Principal.Become(organization.Id, owner.Id, Role.Owner, owner.Email);
        return (organization.Id, owner.Id);
    }

    /// <summary>Adds somebody to an organization in a given role.</summary>
    public static async Task<Guid> MemberAsync(
        IServiceProvider services,
        Guid organizationId,
        string email,
        Role role)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestoDbContext>();
        var credentials = scope.ServiceProvider.GetRequiredService<CredentialService>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();

        var user = await PersonAsync(credentials, email, email);

        context.Users.Add(user);
        context.Memberships.Add(new Membership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        using (writeScope.AllowWritingTo(organizationId))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return user.Id;
    }

    private static async Task<User> PersonAsync(CredentialService credentials, string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = displayName,
            EmailConfirmed = true,
        };

        var problems = await credentials.SetPasswordAsync(user, Password);
        Assert.Empty(problems);

        return user;
    }
}
