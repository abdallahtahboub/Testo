using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Usage;

namespace Autestiq.Application.Tests.Usage;

/// <summary>The usage ledger in the database (Milestone 3.6, gap G6): append-only, each event once.</summary>
public sealed class UsageLedgerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _organization;
    private Guid _project;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_organization, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        _project = await ExecutionSupport.ProjectAsync(_database, _organization, "SHOP");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private UsageEvent Event(string key, long quantity = 1) => new()
    {
        OrganizationId = _organization,
        Category = UsageCategory.Run,
        Quantity = quantity,
        Billable = true,
        Period = "2026-11",
        OccurredAt = DateTimeOffset.UtcNow,
        IdempotencyKey = key,
        Source = "test",
    };

    private async Task SaveAsync(params UsageEvent[] events)
    {
        await using var database = _database.NewContext();
        database.UsageEvents.AddRange(events);

        using (_database.WriteScope.AllowWritingTo(_organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheSameEvent_IsStoredOnce_TheDatabaseRefusesASecond()
    {
        await SaveAsync(Event("run:abc:Run"));

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(Event("run:abc:Run")));
    }

    [Fact]
    public async Task AnEvent_CannotBeChanged()
    {
        await SaveAsync(Event("run:def:Run"));

        await using var database = _database.NewContext();
        _database.Principal.OrganizationId = _organization;
        var stored = await database.UsageEvents.SingleAsync(usage => usage.IdempotencyKey == "run:def:Run", TestContext.Current.CancellationToken);
        stored.Quantity = 0;

        using (_database.WriteScope.AllowWritingTo(_organization))
        {
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Contains("usage ledger is append-only", refusal.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(RunStatus.Passed, 2_000, true)]
    [InlineData(RunStatus.Failed, 2_000, true)]
    [InlineData(RunStatus.InfrastructureError, 2_000, false)]
    [InlineData(RunStatus.Failed, 0, false)]
    public async Task ARunsResult_RecordsItsUsageOnce_BillableAsTheRuleSays(RunStatus status, long bytes, bool billable)
    {
        var options = new ChangeableOptions(new ExecutionOptions
        {
            WorkerPath = ExecutionSupport.ReportingWorker(status, bytes, seconds: 42),
            MaxConcurrentRuns = 1,
        });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, ExecutionSupport.Usage(), TimeProvider.System, NullLogger<RunDispatcher>.Instance);
        var runId = await ExecutionSupport.RunAsync(_database, _organization, _project);

        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(status, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, status, TimeSpan.FromSeconds(30)));
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }

        await using var database = _database.NewContext();
        var events = await database.UsageEvents.IgnoreQueryFilters().Where(usage => usage.RunId == runId).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, events.Count(usage => usage.Category == UsageCategory.Run));
        Assert.Equal(42, events.Single(usage => usage.Category == UsageCategory.BrowserSeconds).Quantity);
        Assert.All(events, usage => Assert.Equal(billable, usage.Billable));
    }

    [Fact]
    public async Task CurrentMonth_AddsUpBillableSeconds_AndRoundsTheMonthUpOnce()
    {
        var month = Autestiq.Application.Usage.UsageMeter.PeriodOf(DateTimeOffset.UtcNow);

        await SaveAsync(
            Billed(Event("a"), UsageCategory.BrowserSeconds, 61, month, true),
            Billed(Event("b"), UsageCategory.BrowserSeconds, 30, month, true),
            Billed(Event("c"), UsageCategory.BrowserSeconds, 500, month, false),
            Billed(Event("d"), UsageCategory.Run, 1, month, true),
            Billed(Event("e"), UsageCategory.BrowserSeconds, 999, "2026-10", true));

        await using var services = _database.NewServices();
        _database.Principal.Become(_organization, Guid.CreateVersion7(), Role.Owner);
        using var scope = services.CreateScope();

        var usage = await scope.ServiceProvider.GetRequiredService<IUsageService>().GetCurrentMonthAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(usage);
        Assert.Equal(month, usage.Month);
        Assert.Equal(2, usage.BrowserMinutesUsed);      // 91 billable seconds this month, rounded up once
        Assert.Equal(100, usage.BrowserMinutesAllowed); // "trial" is the Developer plan
        Assert.Equal(1, usage.BillableRuns);
    }

    private static UsageEvent Billed(UsageEvent usage, UsageCategory category, long quantity, string period, bool billable)
    {
        usage.Category = category;
        usage.Quantity = quantity;
        usage.Period = period;
        usage.Billable = billable;
        return usage;
    }
}
