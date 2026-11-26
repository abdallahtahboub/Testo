using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Contracts.Identity;
using Autestiq.Modules.Settings.Pages;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Settings;

/// <summary>
/// The organization page (E1.4). The service decides who may see it; these check that the page shows what it is given,
/// says the plan's project limit or that there is none, says it changes nothing, and shows nothing it was not given.
/// </summary>
public class OrganizationPageTests : DesignSystemTestContext
{
    private readonly FakeOrganizations _organizations = new();

    public OrganizationPageTests() => Services.AddSingleton<IOrganizationService>(_organizations);

    [Fact]
    public void Render_ShowsWhoTheOrganizationIs_AndWhatItsPlanAllows()
    {
        var page = Render<OrganizationPage>();

        Assert.Equal("Northwind Traders", page.Find("[data-testid='organization-name']").TextContent);
        Assert.Equal("eu", page.Find("[data-testid='organization-region']").TextContent);
        Assert.Equal("Each member's choice", page.Find("[data-testid='organization-two-factor']").TextContent);
        Assert.Equal("Developer, version 1", page.Find("[data-testid='organization-plan-name']").TextContent);
        Assert.Equal("1 of 1", page.Find("[data-testid='organization-projects']").TextContent);
        Assert.Equal("6", page.Find("[data-testid='organization-members']").TextContent);
        Assert.Contains("northwind", page.Markup);
        Assert.Contains("1,200", page.Find("[data-testid='organization-plan']").TextContent);
    }

    [Fact]
    public void Render_APlanWithoutAProjectLimit_SaysSo()
    {
        _organizations.Profile = _organizations.Profile! with { MaxProjects = null, ActiveProjects = 14, RequiresTwoFactor = true };

        var page = Render<OrganizationPage>();

        Assert.Equal("14, no limit", page.Find("[data-testid='organization-projects']").TextContent);
        Assert.Equal("Required for every member", page.Find("[data-testid='organization-two-factor']").TextContent);
    }

    [Fact]
    public void Render_SaysItChangesNothing_AndOffersNoControlToChange()
    {
        var page = Render<OrganizationPage>();

        Assert.Contains("does not change them yet", page.Find("[data-testid='organization-read-only']").TextContent);
        Assert.Contains("Acme QA team", page.Find("[data-testid='organization-read-only']").TextContent);
        Assert.Empty(page.FindAll("input, select, textarea, form"));
        Assert.Empty(page.FindAll("button"));
    }

    [Fact]
    public void Render_LinksToThisMonthsUsage()
    {
        var page = Render<OrganizationPage>();

        Assert.NotNull(page.Find("a[href='/app/settings/usage']"));
    }

    [Fact]
    public void Render_WhenTheServiceGivesNothing_ShowsNothingOfTheOrganization()
    {
        _organizations.Profile = null;

        var page = Render<OrganizationPage>();

        Assert.NotNull(page.Find("[data-testid='organization-not-allowed']"));
        Assert.DoesNotContain("Northwind", page.Markup);
    }

    [Fact]
    public void Render_WhenReadingFails_SaysSo()
    {
        _organizations.Fail = true;

        var page = Render<OrganizationPage>();

        Assert.Contains("The organization could not be read", page.Markup);
    }

    [Fact]
    public void Render_InGerman_UsesTheGermanTexts()
    {
        using var german = new CultureScope("de");

        var page = Render<OrganizationPage>();

        Assert.Equal("Organisation", page.Find("h1").TextContent.Trim());
        Assert.Equal("1 von 1", page.Find("[data-testid='organization-projects']").TextContent);
        Assert.Contains("1.200", page.Find("[data-testid='organization-plan']").TextContent);
    }

    private sealed class FakeOrganizations : IOrganizationService
    {
        public OrganizationProfile? Profile { get; set; } = new(
            "Northwind Traders",
            "northwind",
            "eu",
            RequiresTwoFactor: false,
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            "Developer",
            1,
            new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero),
            MonthlyBrowserMinutes: 1200,
            ConcurrentRuns: 1,
            MaxProjects: 1,
            ActiveProjects: 1,
            ActiveMembers: 6);

        public bool Fail { get; set; }

        public Task<OrganizationProfile?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Fail ? throw new InvalidOperationException("The database is not there.") : Task.FromResult(Profile);
    }
}
