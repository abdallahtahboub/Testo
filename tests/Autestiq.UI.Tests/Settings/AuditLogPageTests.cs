using Microsoft.AspNetCore.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Identity;
using Autestiq.Modules.Settings.Pages;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Settings;

/// <summary>
/// The audit log page (E1.2). The service decides who may read and narrows the entries; these check that the page asks
/// for exactly what was chosen, starts again from the first page when a filter changes, says why a backwards range finds
/// nothing, and offers nothing to a person who may not read the log.
/// </summary>
public class AuditLogPageTests : DesignSystemTestContext
{
    private readonly FakeLog _log = new();
    private readonly Person _person = new(canRead: true);

    public AuditLogPageTests()
    {
        Services.AddSingleton<IAuditLog>(_log);
        Services.AddSingleton<ICurrentUser>(_person);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    [Fact]
    public void Render_ListsTheEntries_NewestFirst_InUtc_WithWhoWhatAndChange()
    {
        var page = Render<AuditLogPage>();

        var rows = page.FindAll("[data-testid='audit-entry']");
        Assert.Equal(2, rows.Count);
        Assert.Contains("2026-11-21 10:15:00", rows[0].TextContent);
        Assert.Contains("sven@northwind.example", rows[0].TextContent);
        Assert.Contains("member.role_changed", rows[0].TextContent);
        Assert.Contains("QaEngineer", rows[0].TextContent);
        Assert.Contains("QaLead", rows[0].TextContent);
        Assert.Contains("The system", rows[1].TextContent);
        Assert.Equal(new AuditQuery(1, 50), _log.Queries.Single());
    }

    [Fact]
    public void Render_WithoutThePermission_OffersNothing_AndAsksForNothing()
    {
        _person.CanRead = false;

        var page = Render<AuditLogPage>();

        Assert.NotNull(page.Find("[data-testid='audit-not-allowed']"));
        Assert.Empty(page.FindAll("[data-testid='audit-entries']"));
        Assert.Empty(page.FindAll("[data-testid='filter-person']"));
        Assert.Empty(_log.Queries);
        Assert.Equal(0, _log.ChoicesRead);
    }

    [Fact]
    public void Filters_OfferThePeopleAndActionsInTheLog()
    {
        var page = Render<AuditLogPage>();

        var people = page.FindAll("[data-testid='filter-person'] option").Select(option => option.GetAttribute("value")).ToList();
        var actions = page.FindAll("[data-testid='filter-action'] option").Select(option => option.GetAttribute("value")).ToList();

        Assert.Equal(["", "sven@northwind.example", "tom@northwind.example"], people);
        Assert.Equal(["", "invitation.sent", "member.role_changed"], actions);
    }

    [Fact]
    public void Filters_AskForWhatWasChosen_FromTheFirstPage()
    {
        var page = Render<AuditLogPage>();

        page.Find("[data-testid='audit-pages'] button[aria-label='Next page']").Click();
        Assert.Equal(2, _log.Queries[^1].Page);

        page.Find("[data-testid='filter-person']").Change("sven@northwind.example");
        page.Find("[data-testid='filter-action']").Change("member.role_changed");
        page.Find("input[data-testid='filter-from']").Change("2026-11-20");
        page.Find("input[data-testid='filter-to']").Change("2026-11-22");

        Assert.Equal(
            new AuditQuery(1, 50, "sven@northwind.example", "member.role_changed", new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 22)),
            _log.Queries[^1]);
    }

    [Fact]
    public void Clear_DropsEveryFilter()
    {
        var page = Render<AuditLogPage>();
        page.Find("[data-testid='filter-action']").Change("invitation.sent");

        page.Find("[data-testid='filter-clear']").Click();

        Assert.Equal(new AuditQuery(1, 50), _log.Queries[^1]);
        Assert.Empty(page.FindAll("[data-testid='filter-clear']"));
    }

    [Fact]
    public void ABackwardsRange_IsSaidInWords_AndNotAskedFor()
    {
        var page = Render<AuditLogPage>();
        var asked = _log.Queries.Count;

        page.Find("input[data-testid='filter-from']").Change("2026-11-22");
        page.Find("input[data-testid='filter-to']").Change("2026-11-20");

        Assert.Contains("before the first", page.Find("[data-testid='audit-dates-problem']").TextContent);
        Assert.Equal(asked + 1, _log.Queries.Count);
        Assert.NotNull(page.Find("[data-testid='audit-empty']"));
    }

    [Fact]
    public void ANonsenseDay_IsIgnored()
    {
        var page = Render<AuditLogPage>();

        page.Find("input[data-testid='filter-from']").Change("not-a-day");

        Assert.Null(_log.Queries[^1].From);
    }

    [Fact]
    public void Render_InGerman_UsesTheGermanTexts()
    {
        using var german = new CultureScope("de");

        var page = Render<AuditLogPage>();

        Assert.Equal("Audit-Protokoll", page.Find("h1").TextContent.Trim());
        Assert.Contains("Wann (UTC)", page.Markup);
    }

    private sealed class Person(bool canRead) : ICurrentUser
    {
        public bool CanRead { get; set; } = canRead;

        public bool IsSignedIn => true;

        public Guid? UserId => Guid.Empty;

        public string? Email => "mara@northwind.example";

        public string? DisplayName => "Mara Lindqvist";

        public Role? Role => CanRead ? Contracts.Identity.Role.Owner : Contracts.Identity.Role.QaLead;

        public bool Can(Permission permission) => permission != Permission.ReadAuditLog || CanRead;
    }

    private sealed class FakeLog : IAuditLog
    {
        private static readonly AuditRecord[] Entries =
        [
            new(Guid.CreateVersion7(), new DateTimeOffset(2026, 11, 21, 10, 15, 0, TimeSpan.Zero), "sven@northwind.example", "member.role_changed", "Membership", "Tom Becker", "203.0.113.7", "QaEngineer", "QaLead"),
            new(Guid.CreateVersion7(), new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero), null, "invitation.sent", "Invitation", "lena@northwind.example", null, null, null),
        ];

        public List<AuditQuery> Queries { get; } = [];

        public int ChoicesRead { get; private set; }

        public Task<PagedResult<AuditRecord>> ReadAsync(AuditQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            IReadOnlyList<AuditRecord> items = query.Action is null ? Entries : [.. Entries.Where(entry => entry.Action == query.Action)];
            return Task.FromResult(new PagedResult<AuditRecord>(items, 120, query.Page, query.PageSize));
        }

        public Task<AuditFilterChoices> ReadFilterChoicesAsync(CancellationToken cancellationToken = default)
        {
            ChoicesRead++;
            return Task.FromResult(new AuditFilterChoices(["sven@northwind.example", "tom@northwind.example"], ["invitation.sent", "member.role_changed"]));
        }
    }
}
