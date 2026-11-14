using Microsoft.EntityFrameworkCore;
using Testo.Application.Tests.Support;
using Testo.Contracts.Identity;
using Testo.Domain.Audit;
using Testo.Domain.Common;
using Testo.Domain.Execution;
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
    /// <summary>
    /// What a seed is given. The ids of the parents are decided up front so that a run can point
    /// at a real project and a step at a real run: the composite foreign keys are part of what is
    /// being tested, and they refuse anything else.
    /// </summary>
    private readonly record struct SeedContext(Guid OrganizationId, Guid UserId, Guid ProjectId, Guid RunId);

    private static readonly Dictionary<Type, Func<SeedContext, object>> Seeds = new()
    {
        [typeof(Membership)] = seed => new Membership
        {
            OrganizationId = seed.OrganizationId,
            UserId = seed.UserId,
            Role = Role.Owner,
            JoinedAt = DateTimeOffset.UtcNow,
        },
        [typeof(Invitation)] = seed => new Invitation
        {
            OrganizationId = seed.OrganizationId,
            Email = $"invited-{Guid.CreateVersion7():N}@example.com",
            NormalizedEmail = $"INVITED-{Guid.CreateVersion7():N}@EXAMPLE.COM",
            Role = Role.QaEngineer,
            TokenHash = Guid.CreateVersion7().ToByteArray(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        },
        [typeof(AuditEntry)] = seed => new AuditEntry
        {
            OrganizationId = seed.OrganizationId,
            ActorUserId = seed.UserId,
            Action = "test.seeded",
            SubjectType = nameof(Organization),
            At = DateTimeOffset.UtcNow,
        },
        [typeof(Project)] = seed => new Project
        {
            Id = seed.ProjectId,
            OrganizationId = seed.OrganizationId,
            Name = "Shop",
            KeyPrefix = $"K{Random.Shared.Next(1000, 9999)}",
        },
        [typeof(TestRun)] = seed => new TestRun
        {
            Id = seed.RunId,
            OrganizationId = seed.OrganizationId,
            ProjectId = seed.ProjectId,
            Number = Random.Shared.Next(1, 100_000),
            ScenarioId = Contracts.Execution.Scenarios.DemoShopBasket,
            TargetOrigin = "http://127.0.0.1:5090",
            QueuedAt = DateTimeOffset.UtcNow,
        },
        [typeof(TestRunStep)] = seed => new TestRunStep
        {
            OrganizationId = seed.OrganizationId,
            TestRunId = seed.RunId,
            Order = 1,
            Description = "Seeded",
        },
        [typeof(Artifact)] = seed => new Artifact
        {
            OrganizationId = seed.OrganizationId,
            TestRunId = seed.RunId,
            Kind = ArtifactKind.Screenshot,
            StorageKey = $"{seed.OrganizationId:N}/seed/step-001.png",
            ContentType = "image/png",
            CapturedAt = DateTimeOffset.UtcNow,
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
    public async Task Save_IntoAnotherOrganization_IsRefused()
    {
        // D-2. Before the guard checked this, acting as one organization could write a row into
        // another, and the other could read it. Reproduced before the fix; this keeps it fixed.
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();
        database.Projects.Add(new Project { OrganizationId = _second, Name = "Planted", KeyPrefix = "PLANT" });

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("not the one this request is acting in", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_IntoAnotherOrganization_IsNeverVisibleToIt()
    {
        _database.Principal.OrganizationId = _first;

        await using (var database = _database.NewContext())
        {
            database.Projects.Add(new Project { OrganizationId = _second, Name = "Planted", KeyPrefix = "PLANT" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        // The other organization gained nothing. This is the assertion that matters; the message
        // above is only how the refusal reads.
        _database.Principal.OrganizationId = _second;
        await using var asSecond = _database.NewContext();

        Assert.Equal(
            0,
            await asSecond.Projects.CountAsync(
                project => project.Name == "Planted",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_IntoAnotherOrganization_IsAllowedWhenItIsDeclared()
    {
        // The escape hatch the invitation and password-reset flows use. Without it they could not
        // work: both write outside the current organization while nobody is signed in.
        _database.Principal.OrganizationId = _first;

        await using var database = _database.NewContext();
        database.Projects.Add(new Project { OrganizationId = _second, Name = "Declared", KeyPrefix = "DECL" });

        using (_database.WriteScope.AllowWritingTo(_second))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _database.Principal.OrganizationId = _second;
        await using var asSecond = _database.NewContext();

        Assert.Equal(
            1,
            await asSecond.Projects.CountAsync(
                project => project.Name == "Declared",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteScope_AfterTheBlockEnds_RefusesAgain()
    {
        // The permission is a block, not a switch. If it outlived its block it would be worse
        // than no guard at all, because it would read as one.
        _database.Principal.OrganizationId = _first;

        using (_database.WriteScope.AllowWritingTo(_second))
        {
            // Deliberately empty: the permission is taken and given back.
        }

        await using var database = _database.NewContext();
        database.Projects.Add(new Project { OrganizationId = _second, Name = "Late", KeyPrefix = "LATE" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));
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

        var seedContext = new SeedContext(organization.Id, user.Id, Guid.CreateVersion7(), Guid.CreateVersion7());

        foreach (var seed in Seeds.Values)
        {
            database.Add(seed(seedContext));
        }

        // Seeding is the one place a test writes into an organization it is not acting in, and it
        // declares that the same way a use case does (D-2).
        using (_database.WriteScope.AllowWritingTo(organization.Id))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return organization.Id;
    }

}
