using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Plans;
using Autestiq.Domain.Projects;

namespace Autestiq.Application.Tests.Usage;

/// <summary>
/// The plan catalog (Milestone 3.7, gap G9, ADR-0028): versioned plans in the database, organizations on a version,
/// operators alone moving them, and limits read from the version an organization holds.
/// </summary>
public sealed class PlanCatalogTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private Guid _organization;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_organization, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private async Task<PlanEntitlements> EntitlementsAsync()
    {
        await using var database = _database.NewContext();
        return await Entitlements.ForAsync(database, _organization, TestContext.Current.CancellationToken);
    }

    private async Task<PlanChange> MoveAsync(string plan, int? version = null, TimeProvider? clock = null)
    {
        await using var database = _database.NewContext();
        return await PlanAssignment.MoveAsync(database, _database.WriteScope, clock ?? new SetClock(Now), "northwind", plan, version, "ops@autestiq.example", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheMigration_SeedsTheFourPlansFirstVersions_WithTheConfirmedFigures()
    {
        await using var database = _database.NewContext();

        var catalog = await (
                from version in database.PlanVersions
                join plan in database.Plans on version.PlanId equals plan.Id
                orderby version.MonthlyBrowserMinutes
                select new { plan.Code, version.Number, version.MonthlyBrowserMinutes, version.ConcurrentRuns, version.MaxProjects })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [("developer", 1, 100L, 1, (int?)1), ("starter", 1, 500L, 2, 3), ("growth", 1, 2_500L, 5, 10), ("scale", 1, 10_000L, 10, null)],
            catalog.Select(row => (row.Code, row.Number, row.MonthlyBrowserMinutes, row.ConcurrentRuns, row.MaxProjects)));
    }

    [Fact]
    public async Task AVersion_CannotBeChanged()
    {
        await using var database = _database.NewContext();
        var developer = await database.PlanVersions.SingleAsync(version => version.Id == PlanCatalog.DeveloperV1, TestContext.Current.CancellationToken);
        developer.MonthlyBrowserMinutes = 1_000_000;

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Contains("plan catalog is append-only", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheStartingVersion_IsTheLatestDeveloperVersion()
    {
        // Publishing a new Developer version without deciding that new customers get it fails here.
        await using var database = _database.NewContext();

        var latest = await database.PlanVersions
            .Where(version => version.PlanId == PlanCatalog.Developer)
            .OrderByDescending(version => version.Number)
            .Select(version => version.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(latest, PlanCatalog.StartingVersion);
    }

    [Fact]
    public async Task ANewOrganization_StartsOnTheDeveloperPlan_AndItsLimitsComeFromThatVersion()
    {
        var entitlements = await EntitlementsAsync();

        Assert.Equal(new PlanEntitlements("developer", "Developer", 1, 100, 1, 1), entitlements);

        await using var database = _database.NewContext();
        var room = await RunDispatcher.RoomForAsync(database, _organization, Now, TestContext.Current.CancellationToken);
        Assert.Equal(100 * 60, room.SecondsLeft);
    }

    [Fact]
    public async Task MovingToGrowth_ChangesTheLimits_AndIsAuditedWithBothVersions()
    {
        Assert.Equal(PlanChange.Moved, await MoveAsync("growth"));

        var entitlements = await EntitlementsAsync();
        Assert.Equal(("growth", 2_500L, 5, (int?)10), (entitlements.PlanCode, entitlements.MonthlyBrowserMinutes, entitlements.ConcurrentRuns, entitlements.MaxProjects));

        await using var database = _database.NewContext();
        var entry = await database.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(audit => audit.OrganizationId == _organization && audit.Action == "organization.plan_changed", TestContext.Current.CancellationToken);

        Assert.Equal("ops@autestiq.example", entry.ActorEmail);
        Assert.Contains(PlanCatalog.DeveloperV1.ToString(), entry.Before, StringComparison.Ordinal);
        Assert.Contains(PlanCatalog.GrowthV1.ToString(), entry.After, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("platinum", null, PlanChange.NoSuchPlan)]
    [InlineData("growth", 7, PlanChange.NoSuchPlan)]
    [InlineData("developer", null, PlanChange.Unchanged)]
    public async Task Moving_RefusesWhatIsNotThere_AndChangesNothingWhenAlreadyThere(string plan, int? version, PlanChange expected)
    {
        Assert.Equal(expected, await MoveAsync(plan, version));
        Assert.Equal("developer", (await EntitlementsAsync()).PlanCode);
    }

    [Fact]
    public async Task ANewVersionWithLowerLimits_LeavesOrganizationsOnTheOldOne_UntilTheyAreMoved()
    {
        // The owner's choice: an organization keeps its version. A cut does not reach it without a decision.
        await MoveAsync("growth");
        await PublishAsync(PlanCatalog.Growth, number: 2, minutes: 1_000, effectiveFrom: Now.AddDays(-1));

        Assert.Equal((1, 2_500L), ((await EntitlementsAsync()).Version, (await EntitlementsAsync()).MonthlyBrowserMinutes));

        Assert.Equal(PlanChange.Moved, await MoveAsync("growth", version: 2));
        Assert.Equal((2, 1_000L), ((await EntitlementsAsync()).Version, (await EntitlementsAsync()).MonthlyBrowserMinutes));
    }

    [Fact]
    public async Task AVersionThatTakesEffectLater_CannotBeMovedOntoYet()
    {
        await PublishAsync(PlanCatalog.Growth, number: 2, minutes: 3_000, effectiveFrom: Now.AddDays(10));

        Assert.Equal(PlanChange.NotYetEffective, await MoveAsync("growth", version: 2));

        // And "the latest" means the latest in effect, so the plan's code alone gives version 1.
        Assert.Equal(PlanChange.Moved, await MoveAsync("growth"));
        Assert.Equal(1, (await EntitlementsAsync()).Version);
    }

    [Fact]
    public async Task TheUsageService_SaysWhichPlanAndVersionTheLimitsComeFrom()
    {
        await MoveAsync("starter");

        await using var services = _database.NewServices();
        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.Owner);
        using var scope = services.CreateScope();

        var usage = await scope.ServiceProvider.GetRequiredService<IUsageService>().GetCurrentMonthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("Starter", 1, 500L, 2), (usage!.PlanName, usage.PlanVersion, usage.BrowserMinutesAllowed, usage.ConcurrentRunsAllowed));
    }

    [Fact]
    public async Task Projects_UpToThePlansLimit_AndNoFurther_AndScaleHasNone()
    {
        await MoveAsync("starter");

        for (var made = 0; made < 3; made++)
        {
            Assert.True(await MayAddProjectAsync());
            await ExecutionSupport.ProjectAsync(_database, _organization, $"P{made}");
        }

        Assert.False(await MayAddProjectAsync());

        await MoveAsync("scale");
        Assert.True(await MayAddProjectAsync());
    }

    private async Task<bool> MayAddProjectAsync()
    {
        await using var database = _database.NewContext();
        return await Entitlements.MayAddProjectAsync(database, _organization, TestContext.Current.CancellationToken);
    }

    private async Task PublishAsync(Guid plan, int number, long minutes, DateTimeOffset effectiveFrom)
    {
        await using var database = _database.NewContext();
        database.PlanVersions.Add(new PlanVersion
        {
            PlanId = plan,
            Number = number,
            EffectiveFrom = effectiveFrom,
            MonthlyBrowserMinutes = minutes,
            ConcurrentRuns = 5,
            MaxProjects = 10,
        });

        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
