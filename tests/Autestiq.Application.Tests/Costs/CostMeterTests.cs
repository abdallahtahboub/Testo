using Autestiq.Application.Costs;
using Autestiq.Contracts.Usage;
using Autestiq.Domain.Costs;
using Autestiq.Domain.Execution;
using Autestiq.Domain.Usage;

namespace Autestiq.Application.Tests.Costs;

/// <summary>The rules that turn a run's usage into its cost rows (Milestone 3.8, gap G10, ADR-0029).</summary>
public sealed class CostMeterTests
{
    private static readonly TestRun Run = new()
    {
        OrganizationId = Guid.CreateVersion7(),
        ProjectId = Guid.CreateVersion7(),
        Number = 1,
        ScenarioId = "basket",
        TargetOrigin = "http://127.0.0.1:5090",
    };

    private static UsageEvent Used(UsageCategory category, long quantity, bool billable = true) => new()
    {
        OrganizationId = Run.OrganizationId,
        RunId = Run.Id,
        Category = category,
        Quantity = quantity,
        Billable = billable,
        Period = "2026-11",
        IdempotencyKey = $"run:{Run.Id:N}:{category}",
        Source = "test",
    };

    private static PriceVersion Prices(params UnitPrice[] prices)
    {
        var version = new PriceVersion { Number = 1, Currency = "USD", PublishedBy = "test", Note = "test" };

        foreach (var price in prices)
        {
            version.Prices.Add(price);
        }

        return version;
    }

    private static UnitPrice Price(string item, decimal price, long per, bool estimate = false) =>
        new() { Item = item, Price = price, Per = per, Estimate = estimate };

    [Theory]
    [InlineData(3_600, 0.10)]
    [InlineData(42, 0.001167)]   // 42 / 3600 × 0.10 = 0.0011666…, to a millionth
    [InlineData(1, 0.000028)]
    [InlineData(0, 0)]
    public void ABrowserHourPrice_CostsEachSecond_ToAMillionth(long seconds, decimal expected) =>
        Assert.Equal(expected, Price(CostItems.BrowserSecond, 0.10m, PriceCatalog.SecondsPerHour).CostOf(seconds));

    [Fact]
    public void ARunsUsage_BecomesOneRowPerPricedItem_WithThePriceAndVersionItUsed()
    {
        var prices = Prices(
            Price(CostItems.BrowserSecond, 0.10m, PriceCatalog.SecondsPerHour, estimate: true),
            Price(CostItems.StorageByteMonth, 0.023m, PriceCatalog.BytesPerGigabyte, estimate: true));

        var rows = CostMeter.ForRun(Run, [Used(UsageCategory.Run, 1), Used(UsageCategory.BrowserSeconds, 3_600), Used(UsageCategory.ArtifactBytes, PriceCatalog.BytesPerGigabyte)], prices);

        Assert.Equal([CostItems.BrowserSecond, CostItems.StorageByteMonth], rows.Select(row => row.Item));
        Assert.Equal([0.10m, 0.023m], rows.Select(row => row.Amount));
        Assert.All(rows, row =>
        {
            Assert.Equal(prices.Id, row.PriceVersionId);
            Assert.Equal(Run.OrganizationId, row.OrganizationId);
            Assert.Equal("USD", row.Currency);
            Assert.True(row.Estimate);
            Assert.Equal(CostMeter.KeyFor(Run.Id, row.Item), row.IdempotencyKey);
        });
    }

    [Fact]
    public void ARunThatIsNotBillable_StillCostsUs_AndSaysSo()
    {
        var prices = Prices(Price(CostItems.BrowserSecond, 0.10m, PriceCatalog.SecondsPerHour));

        var row = Assert.Single(CostMeter.ForRun(Run, [Used(UsageCategory.BrowserSeconds, 600, billable: false)], prices));

        Assert.False(row.Billable);
        Assert.True(row.Amount > 0);
    }

    [Fact]
    public void AnItemWithoutAPrice_IsNamed_NotCostedAtNothing()
    {
        var unpriced = new List<string>();

        var rows = CostMeter.ForRun(Run, [Used(UsageCategory.BrowserSeconds, 60)], Prices(), unpriced);

        Assert.Empty(rows);
        Assert.Equal([CostItems.BrowserSecond], unpriced);
    }
}
