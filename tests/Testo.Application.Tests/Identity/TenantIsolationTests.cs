using Microsoft.EntityFrameworkCore;
using Testo.Application.Tests.Support;
using Testo.Contracts.Identity;
using Testo.Domain.Audit;
using Testo.Domain.Common;
using Testo.Domain.Identity;
using Testo.Domain.Projects;

namespace Testo.Application.Tests.Identity;

/// <summary>
/// Two organizations, each with a row of every kind, and neither able to see the other's.
/// </summary>
/// <remarks>
/// This is the test the product cannot afford to fail. A leak here is the end of a business that
/// sells to companies, which is why isolation is built three times over
/// (<c>docs/domain-model.md</c> §4) and why this asks the question for every entity rather than
/// for the ones somebody remembered.
/// </remarks>
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _first;
    private Guid _second;

    /// <summary>
    /// A row of each kind, for one organization. Every entity inside the tenant boundary needs an
    /// entry here, and <see cref="Seeding_Always_CoversEveryTenantEntity"/> fails if one is
    /// missing — so an entity added in a later milestone cannot quietly go untested.
    /// </summary>
    private static readonly Dictionary<Type, Func<Guid, Guid, object>> Seeds = new()
    {
        [typeof(Membership)] = (organizationId, userId) => new Membership
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = Role.Owner,
            JoinedAt = DateTimeOffset.UtcNow,
        },
        [typeof(Invitation)] = (organizationId, _) => new Invitation
        {
            OrganizationId = organizationId,
            Email = $"invited-{Guid.CreateVersion7():N}@example.com",
            NormalizedEmail = $"INVITED-{Guid.CreateVersion7():N}@EXAMPLE.COM",
            Role = Role.QaEngineer,
            TokenHash = Guid.CreateVersion7().ToByteArray(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        },
        [typeof(AuditEntry)] = (organizationId, userId) => new AuditEntry
        {
            OrganizationId = organizationId,
            ActorUserId = userId,
            Action = "test.seeded",
            SubjectType = nameof(Organization),
            At = DateTimeOffset.UtcNow,
        },
        [typeof(Project)] = (organizationId, _) => new Project
        {
            OrganizationId = organizationId,
            Name = "Shop",
            KeyPrefix = $"K{Random.Shared.Next(1000, 9999)}",
        },
    };

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        _first = await SeedOrganizationAsync("first");
        _second = await SeedOrganizationAsync("second");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    public static TheoryData<Type> TenantEntities => [.. Seeds.Keys];

    [Theory]
    [MemberData(nameof(TenantEntities))]
    public async Task Query_AsOneOrganization_NeverReturnsAnotherOrganizationsRows(Type entityType)
    {
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();

        var visible = database.VisibleRows(entityType);
        var mine = visible.Count(row => row.OrganizationId == _first);

        Assert.True(
            visible.Count > 0,
            $"The test seeded no {entityType.Name}, so it would pass without proving anything.");

        Assert.True(
            visible.Count == mine,
            $"Reading {entityType.Name} as one organization returned {visible.Count} rows, "
            + $"of which only {mine} belong to it.");
    }

    [Fact]
    public async Task Seeding_Always_CoversEveryTenantEntity()
    {
        await using var database = _database.NewContext();

        var insideTheBoundary = database.Model.GetEntityTypes()
            .Select(entity => entity.ClrType)
            .Where(type => type.IsAssignableTo(typeof(IBelongsToOrganization)))
            .ToHashSet();

        Assert.Equal(insideTheBoundary, Seeds.Keys.ToHashSet());
    }

    [Fact]
    public async Task Save_WithNoOrganizationInContext_Refuses()
    {
        _database.Principal.OrganizationId = null;

        await using var database = _database.NewContext();

        database.Projects.Add(new Project { Name = "Nowhere", KeyPrefix = "NOW" });

        // The guard would rather fail loudly than write a row no customer owns.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_MovingARowToAnotherOrganization_Refuses()
    {
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();

        var membership = await database.Memberships.FirstAsync(TestContext.Current.CancellationToken);
        membership.OrganizationId = _second;

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("cannot be moved between organizations", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_MovingAProject_IsRefusedByTheKeyBeforeTheGuardIsReached()
    {
        // Projects carry an alternate key of (organization_id, id), so that the entities of
        // Milestone 5 can point at them with a foreign key the database itself checks. A side
        // effect is that Entity Framework refuses to change the column at all — a fourth refusal,
        // earlier than the guard.
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();

        var project = await database.Projects.FirstAsync(TestContext.Current.CancellationToken);
        project.OrganizationId = _second;

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("part of a key", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_ChangingAnAuditEntry_Refuses()
    {
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();

        var entry = await database.AuditEntries.FirstAsync(TestContext.Current.CancellationToken);
        entry.Action = "something.else";

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_DeletingAnAuditEntry_Refuses()
    {
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();

        database.AuditEntries.Remove(await database.AuditEntries.FirstAsync(TestContext.Current.CancellationToken));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedOrganizationAsync(string name)
    {
        // Seeded with no organization in context, which is what creating a customer actually is.
        _database.Principal.OrganizationId = null;

        await using var database = _database.NewContext();

        var organization = new Organization { Name = name, Slug = name, DataRegion = "eu-central" };
        var user = new User
        {
            Email = $"{name}@example.com",
            NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.COM",
            DisplayName = name,
        };

        database.Organizations.Add(organization);
        database.Users.Add(user);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var seed in Seeds.Values)
        {
            database.Add(seed(organization.Id, user.Id));
        }

        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organization.Id;
    }

}
