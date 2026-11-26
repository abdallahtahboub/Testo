using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Contracts.Usage;
using Autestiq.Modules.Settings.Pages;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Settings;

/// <summary>
/// The usage page (E1.3). The ledger and the plan decide the figures; these check that the page shows them, says in words
/// how much is left and when the month starts again, says when new runs are refused, and shows nothing it was not given.
/// </summary>
public class UsagePageTests : DesignSystemTestContext
{
    private readonly FakeUsage _usage = new();

    public UsagePageTests() => Services.AddSingleton<IUsageService>(_usage);

    [Fact]
    public void Render_ShowsTheMonthsFigures_AgainstThePlan()
    {
        _usage.Current = Usage(used: 37, allowed: 600);

        var page = Render<UsagePage>();

        Assert.Contains("37 of 600", page.Find("[data-testid='usage-minutes']").TextContent);
        Assert.Contains("12", page.Find("[data-testid='usage-runs']").TextContent);
        Assert.Contains("2", page.Find("[data-testid='usage-concurrent']").TextContent);
        Assert.Contains("Developer, version 1", page.Find("[data-testid='usage-plan']").TextContent);
        Assert.Contains("November 2026", page.Find("[data-testid='usage-month']").TextContent);
        Assert.Contains("December 1, 2026", page.Find("[data-testid='usage-month']").TextContent);
        Assert.Equal("563 minutes left this month.", page.Find("[data-testid='usage-state']").TextContent);
    }

    [Fact]
    public void Render_TheMeter_StopsAtTheAllowance_AndIsDescribedInWords()
    {
        _usage.Current = Usage(used: 700, allowed: 600);

        var page = Render<UsagePage>();

        var meter = page.Find("[data-testid='usage-meter']");
        Assert.Equal("600", meter.GetAttribute("max"));
        Assert.Equal("600", meter.GetAttribute("value"));
        Assert.Equal("usage-state", meter.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Render_NearlyUsedUp_SaysSo()
    {
        _usage.Current = Usage(used: 500, allowed: 600);

        var page = Render<UsagePage>();

        Assert.StartsWith("Nearly used up: 100 minutes left", page.Find("[data-testid='usage-state']").TextContent);
        Assert.Null(page.Find("[data-testid='usage-state']").GetAttribute("role"));
    }

    [Theory]
    [InlineData(600, 600)]
    [InlineData(650, 600)]
    [InlineData(0, 0)]
    public void Render_UsedUp_SaysNewRunsAreRefused_AsAnAlert(long used, long allowed)
    {
        _usage.Current = Usage(used, allowed);

        var page = Render<UsagePage>();

        var state = page.Find("[data-testid='usage-state']");
        Assert.Contains("New runs are refused until", state.TextContent);
        Assert.Equal("alert", state.GetAttribute("role"));
    }

    [Fact]
    public void Render_WhenTheServiceGivesNothing_ShowsNoFigures()
    {
        _usage.Current = null;

        var page = Render<UsagePage>();

        Assert.NotNull(page.Find("[data-testid='usage-not-allowed']"));
        Assert.Empty(page.FindAll("[data-testid='usage-minutes']"));
    }

    [Fact]
    public void Render_WhenReadingFails_SaysSo_AndOffersToTryAgain()
    {
        _usage.Fail = true;

        var page = Render<UsagePage>();

        Assert.Contains("Usage could not be read", page.Markup);
        _usage.Fail = false;
        _usage.Current = Usage(1, 600);
        page.Find("button").Click();
        Assert.Contains("1 of 600", page.Find("[data-testid='usage-minutes']").TextContent);
    }

    [Fact]
    public void Render_InGerman_UsesTheGermanTexts()
    {
        using var german = new CultureScope("de");
        _usage.Current = Usage(1234, 6000);

        var page = Render<UsagePage>();

        Assert.Equal("Verbrauch", page.Find("h1").TextContent.Trim());
        Assert.Contains("1.234 von 6.000", page.Find("[data-testid='usage-minutes']").TextContent);
        Assert.Contains("November 2026", page.Find("[data-testid='usage-month']").TextContent);
    }

    private static OrganizationUsage Usage(long used, long allowed) => new("2026-11", used, allowed, 12, 2, "Developer", 1);

    private sealed class FakeUsage : IUsageService
    {
        public OrganizationUsage? Current { get; set; }

        public bool Fail { get; set; }

        public Task<OrganizationUsage?> GetCurrentMonthAsync(CancellationToken cancellationToken = default) =>
            Fail ? throw new InvalidOperationException("The database is not there.") : Task.FromResult(Current);
    }
}
