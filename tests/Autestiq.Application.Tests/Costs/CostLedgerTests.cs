using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Costs;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Costs;
using Autestiq.Domain.Usage;

namespace Autestiq.Application.Tests.Costs;

/// <summary>
/// The price book and the cost ledger in the database (Milestone 3.8, gaps G10 and G11, ADR-0029): runs priced at the
/// price in force when they finished, rows that add up to the run's total, and history that a new price never rewrites.
/// </summary>
public sealed class CostLedgerTests : IAsyncLifetime
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

    private async Task<Guid> CarryOutAsync(RunStatus status, long bytes, int seconds)
    {
        var options = new ChangeableOptions(new ExecutionOptions
        {
            WorkerPath = ExecutionSupport.ReportingWorker(status, bytes, seconds),
            MaxConcurrentRuns = 1,
        });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);
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

        return runId;
    }

    private async Task<List<RunCost>> CostsOfAsync(Guid runId)
    {
        await using var database = _database.NewContext();
        return await database.RunCosts.IgnoreQueryFilters().Where(cost => cost.RunId == runId).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheMigration_SeedsTheFirstPriceVersion_WithEstimatesMarkedAndCurrentModelPrices()
    {
        await using var database = _database.NewContext();

        var first = await database.PriceVersions.Include(version => version.Prices).SingleAsync(TestContext.Current.CancellationToken);
        var prices = first.Prices.ToDictionary(price => price.Item);

        Assert.Equal(("USD", 1), (first.Currency, first.Number));
        Assert.Equal((0.10m, PriceCatalog.SecondsPerHour, true), (prices[CostItems.BrowserSecond].Price, prices[CostItems.BrowserSecond].Per, prices[CostItems.BrowserSecond].Estimate));
        Assert.Equal((0.023m, PriceCatalog.BytesPerGigabyte, true), (prices[CostItems.StorageByteMonth].Price, prices[CostItems.StorageByteMonth].Per, prices[CostItems.StorageByteMonth].Estimate));
        Assert.Equal((2.00m, false), (prices[CostItems.ModelInputToken("claude-sonnet-5-5")].Price, prices[CostItems.ModelInputToken("claude-sonnet-5-5")].Estimate));
        Assert.Equal(10.00m, prices[CostItems.ModelOutputToken("claude-sonnet-5-5")].Price);
    }

    [Theory]
    [InlineData(RunStatus.Passed, 2_000, true)]
    [InlineData(RunStatus.InfrastructureError, 2_000, false)]
    public async Task ARunsResult_RecordsItsCost_AndTheRowsAddUpToTheRunsTotal(RunStatus status, long bytes, bool billable)
    {
        var runId = await CarryOutAsync(status, bytes, seconds: 720);

        var costs = await CostsOfAsync(runId);
        var run = await ExecutionSupport.StoredAsync(_database, runId);

        var browser = Assert.Single(costs, cost => cost.Item == CostItems.BrowserSecond);
        Assert.Equal(720, browser.Quantity);
        Assert.Equal(0.02m, browser.Amount); // 720 s at $0.10 an hour
        Assert.Equal(PriceCatalog.FirstVersionId, browser.PriceVersionId);
        Assert.All(costs, cost => Assert.Equal(billable, cost.Billable));
        Assert.Equal(costs.Sum(cost => cost.Amount), run.Cost);
    }

    [Fact]
    public async Task ANewPriceVersion_DoesNotRewriteACostAlreadyRecorded_NorPriceARunThatFinishedBeforeIt()
    {
        var before = await CarryOutAsync(RunStatus.Passed, 2_000, seconds: 3_600);

        await using (var database = _database.NewContext())
        {
            var (outcome, _) = await PriceBook.PublishAsync(
                database, TimeProvider.System, [new PriceChange(CostItems.BrowserSecond, 1.00m, PriceCatalog.SecondsPerHour, Estimate: false)],
                effectiveFrom: null, "test: ten times dearer", "ops@autestiq.example", TestContext.Current.CancellationToken);
            Assert.Equal(PricePublication.Published, outcome);
        }

        // The worker says its run finished on 2026-11-16 at 13:00, before the new version took effect.
        var after = await CarryOutAsync(RunStatus.Passed, 2_000, seconds: 3_600);

        Assert.Equal(0.10m, (await CostsOfAsync(before)).Single(cost => cost.Item == CostItems.BrowserSecond).Amount);
        Assert.Equal(0.10m, (await CostsOfAsync(after)).Single(cost => cost.Item == CostItems.BrowserSecond).Amount);
        Assert.Equal(0.10m, (await ExecutionSupport.StoredAsync(_database, before)).Cost);
    }

    [Fact]
    public async Task ThePriceInForce_IsTheLatestVersionThatHadTakenEffect()
    {
        await using var database = _database.NewContext();
        var (_, second) = await PriceBook.PublishAsync(
            database, new SetClock(PriceCatalog.Launched), [new PriceChange(CostItems.BrowserSecond, 0.20m, PriceCatalog.SecondsPerHour, Estimate: false)],
            new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), "test", "ops@autestiq.example", TestContext.Current.CancellationToken);

        Assert.Equal(1, (await CostMeter.InForceAsync(database, new DateTimeOffset(2026, 11, 30, 23, 59, 59, TimeSpan.Zero), TestContext.Current.CancellationToken))?.Number);
        Assert.Equal(second?.Id, (await CostMeter.InForceAsync(database, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken))?.Id);
        Assert.Null(await CostMeter.InForceAsync(database, PriceCatalog.Launched.AddTicks(-1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ANewVersion_KeepsThePricesItDidNotChange()
    {
        await using var database = _database.NewContext();
        var (_, second) = await PriceBook.PublishAsync(
            database, TimeProvider.System, [new PriceChange(CostItems.StorageByteMonth, 0.02m, PriceCatalog.BytesPerGigabyte, Estimate: false)],
            null, "test", "ops@autestiq.example", TestContext.Current.CancellationToken);

        Assert.NotNull(second);
        Assert.Equal(2, second.Number);
        Assert.Equal(PriceCatalog.FirstPrices.Count, second.Prices.Count);
        Assert.Equal(0.10m, second.Prices.Single(price => price.Item == CostItems.BrowserSecond).Price);
        Assert.Equal((0.02m, false), (second.Prices.Single(price => price.Item == CostItems.StorageByteMonth).Price, second.Prices.Single(price => price.Item == CostItems.StorageByteMonth).Estimate));
    }

    [Fact]
    public async Task AVersionCannotTakeEffectInThePast_NorRepeatTheLatest()
    {
        await using var database = _database.NewContext();
        var browser = new PriceChange(CostItems.BrowserSecond, 0.50m, PriceCatalog.SecondsPerHour, Estimate: false);

        var (past, _) = await PriceBook.PublishAsync(database, TimeProvider.System, [browser], DateTimeOffset.UtcNow.AddDays(-1), "test", "ops", TestContext.Current.CancellationToken);
        var (same, _) = await PriceBook.PublishAsync(database, TimeProvider.System, [new PriceChange(CostItems.BrowserSecond, 0.10m, PriceCatalog.SecondsPerHour, Estimate: true)], null, "test", "ops", TestContext.Current.CancellationToken);

        Assert.Equal(PricePublication.InThePast, past);
        Assert.Equal(PricePublication.Unchanged, same);
        Assert.Equal(1, await database.PriceVersions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task APriceVersion_CannotBeChanged()
    {
        await using var database = _database.NewContext();
        var price = await database.UnitPrices.FirstAsync(TestContext.Current.CancellationToken);
        price.Price = 0m;

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Contains("price book is append-only", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACostRow_CannotBeChanged_NorRecordedTwice()
    {
        var runId = await CarryOutAsync(RunStatus.Passed, 2_000, seconds: 60);

        await using (var database = _database.NewContext())
        {
            var row = await database.RunCosts.IgnoreQueryFilters().FirstAsync(cost => cost.RunId == runId, TestContext.Current.CancellationToken);
            row.Amount = 0m;

            using (_database.WriteScope.AllowWritingTo(_organization))
            {
                var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
                Assert.Contains("cost ledger is append-only", refusal.Message, StringComparison.Ordinal);
            }
        }

        await using (var database = _database.NewContext())
        {
            var row = await database.RunCosts.IgnoreQueryFilters().AsNoTracking().FirstAsync(cost => cost.RunId == runId, TestContext.Current.CancellationToken);
            database.RunCosts.Add(new RunCost
            {
                OrganizationId = row.OrganizationId,
                RunId = row.RunId,
                Item = row.Item,
                Quantity = row.Quantity,
                Price = row.Price,
                Per = row.Per,
                Amount = row.Amount,
                Currency = row.Currency,
                PriceVersionId = row.PriceVersionId,
                Billable = row.Billable,
                Period = row.Period,
                IdempotencyKey = row.IdempotencyKey,
            });

            using (_database.WriteScope.AllowWritingTo(_organization))
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public async Task TheCostReport_AddsUpAMonth_PerOrganizationAndInTotal()
    {
        await CarryOutAsync(RunStatus.Passed, 2_000, seconds: 3_600);          // billable: 60 minutes, $0.10
        await CarryOutAsync(RunStatus.InfrastructureError, 2_000, seconds: 1_800); // ours: $0.05

        await using var database = _database.NewContext();
        var period = Autestiq.Application.Usage.UsageMeter.PeriodOf(DateTimeOffset.UtcNow);

        var lines = await CostReport.ForMonthAsync(database, period, TestContext.Current.CancellationToken);

        var northwind = Assert.Single(lines, line => line.Organization == "northwind");
        Assert.Equal((2, 1, 60L), (northwind.Runs, northwind.BillableRuns, northwind.BillableMinutes));
        Assert.Equal(0.15m, northwind.Cost);
        Assert.Equal(0.05m, northwind.OurFailuresCost);
        Assert.Equal(0.075m, northwind.CostPerRun);
        Assert.Equal(0.0025m, northwind.CostPerBillableMinute);
        Assert.Equal(0.3333m, northwind.OurFailuresShare);
        Assert.True(northwind.FromEstimates);

        var total = lines[^1];
        Assert.Null(total.Organization);
        Assert.Equal(0.15m, total.Cost);

        var text = CostReportText.Write(period, lines, eurosPerDollar: 0.92m);
        Assert.Contains("ESTIMATES", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UsageEvents_AreTheQuantitiesTheCostRowsPrice()
    {
        var runId = await CarryOutAsync(RunStatus.Passed, 2_000, seconds: 90);

        await using var database = _database.NewContext();
        var seconds = await database.UsageEvents.IgnoreQueryFilters()
            .SingleAsync(usage => usage.RunId == runId && usage.Category == UsageCategory.BrowserSeconds, TestContext.Current.CancellationToken);

        Assert.Equal(seconds.Quantity, (await CostsOfAsync(runId)).Single(cost => cost.Item == CostItems.BrowserSecond).Quantity);
    }
}
