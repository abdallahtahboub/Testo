using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Audit;

namespace Autestiq.Application.Tests.Audit;

/// <summary>
/// Reading the audit log through its filters (E1.2): by person, by action and by day, newest first, a page at a time;
/// never for somebody without the permission, and never another organization's entries.
/// </summary>
public sealed class AuditLogTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Day = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;
    private Guid _northwind;
    private Guid _contoso;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _services = _database.NewServices();
        (_contoso, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Contoso");
        (_northwind, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Northwind");

        await using var database = _database.NewContext();
        database.AuditEntries.AddRange(
            Entry(_northwind, "sven@northwind.example", "member.role_changed", Day.AddHours(9)),
            Entry(_northwind, "sven@northwind.example", "invitation.sent", Day.AddDays(1).AddHours(10)),
            Entry(_northwind, "tom@northwind.example", "invitation.sent", Day.AddDays(2).AddHours(23).AddMinutes(59)),
            Entry(_northwind, "Tom@Northwind.example", "signin.failed", Day.AddDays(3)));

        using (_database.WriteScope.AllowWritingTo(_northwind))
        {
            await database.SaveChangesAsync(Cancellation);
        }

        database.AuditEntries.Add(Entry(_contoso, "eve@contoso.example", "invitation.sent", Day.AddDays(1)));

        using (_database.WriteScope.AllowWritingTo(_contoso))
        {
            await database.SaveChangesAsync(Cancellation);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Read_WithNoFilter_IsTheWholeLog_NewestFirst_AndOnlyThisOrganizations()
    {
        var page = await ReadAsync(new AuditQuery());

        Assert.DoesNotContain(page.Items, entry => entry.ActorName == "eve@contoso.example");
        var seeded = page.Items.Where(entry => entry.At >= Day).Select(entry => entry.Action).ToList();
        Assert.Equal(["signin.failed", "invitation.sent", "invitation.sent", "member.role_changed"], seeded);
        Assert.Equal(page.Items.OrderByDescending(entry => entry.At).Select(entry => entry.Id), page.Items.Select(entry => entry.Id));
    }

    [Fact]
    public async Task Read_ByPerson_MatchesTheAddressExactly()
    {
        var page = await ReadAsync(new AuditQuery(Actor: "sven@northwind.example"));

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, entry => Assert.Equal("sven@northwind.example", entry.ActorName));
    }

    [Fact]
    public async Task Read_ByAction_KeepsOnlyThatAction_InThisOrganization()
    {
        var page = await ReadAsync(new AuditQuery(Action: "invitation.sent"));

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, entry => Assert.Equal("invitation.sent", entry.Action));
    }

    [Fact]
    public async Task Read_ByDays_IncludesTheWholeOfTheLastDay_InUtc()
    {
        var from = DateOnly.FromDateTime(Day.AddDays(1).UtcDateTime);
        var to = DateOnly.FromDateTime(Day.AddDays(2).UtcDateTime);

        var page = await ReadAsync(new AuditQuery(From: from, To: to));

        // The entry at 23:59 on the last day is in; the one at midnight after it is not.
        Assert.Equal(["invitation.sent", "invitation.sent"], page.Items.Select(entry => entry.Action));
    }

    [Fact]
    public async Task Read_FiltersTogether_NarrowEachOther()
    {
        var page = await ReadAsync(new AuditQuery(Actor: "tom@northwind.example", Action: "invitation.sent"));

        var entry = Assert.Single(page.Items);
        Assert.Equal(Day.AddDays(2).AddHours(23).AddMinutes(59), entry.At);
    }

    [Fact]
    public async Task Read_APage_HoldsAtMostItsSize_AndCountsEverything()
    {
        var first = await ReadAsync(new AuditQuery(Page: 1, PageSize: 1, Actor: "sven@northwind.example"));
        var second = await ReadAsync(new AuditQuery(Page: 2, PageSize: 1, Actor: "sven@northwind.example"));

        Assert.Equal(2, first.TotalCount);
        Assert.Equal("invitation.sent", Assert.Single(first.Items).Action);
        Assert.Equal("member.role_changed", Assert.Single(second.Items).Action);
    }

    [Fact]
    public async Task Read_AnOversizedOrNonsensePage_IsClamped()
    {
        var page = await ReadAsync(new AuditQuery(Page: -3, PageSize: 100_000));

        Assert.Equal(1, page.Page);
        Assert.Equal(AuditQuery.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task FilterChoices_AreThePeopleAndActionsInThisOrganizationsLog()
    {
        var choices = await ChoicesAsync();

        Assert.Contains("sven@northwind.example", choices.Actors);
        Assert.Contains("Tom@Northwind.example", choices.Actors);
        Assert.DoesNotContain("eve@contoso.example", choices.Actors);
        Assert.Equal(choices.Actions.Order(StringComparer.Ordinal), choices.Actions);
        Assert.Contains("signin.failed", choices.Actions);
        Assert.Equal(choices.Actions.Distinct(), choices.Actions);
    }

    [Theory]
    [InlineData(Role.QaLead)]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task Read_WithoutThePermission_ShowsNothing_AndOffersNoChoices(Role role)
    {
        var person = await Seed.MemberAsync(_services, _northwind, $"{role}@northwind.example".ToLowerInvariant(), role);
        _database.Principal.Become(_northwind, person, role, $"{role}@northwind.example".ToLowerInvariant());

        var page = await ReadAsync(new AuditQuery(Action: "invitation.sent"));
        var choices = await ChoicesAsync();

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(choices.Actors);
        Assert.Empty(choices.Actions);
    }

    [Fact]
    public async Task Read_AnotherOrganizationsActor_FindsNothing()
    {
        var page = await ReadAsync(new AuditQuery(Actor: "eve@contoso.example"));

        Assert.Empty(page.Items);
    }

    private async Task<Contracts.Common.PagedResult<AuditRecord>> ReadAsync(AuditQuery query)
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuditLog>().ReadAsync(query, Cancellation);
    }

    private async Task<AuditFilterChoices> ChoicesAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuditLog>().ReadFilterChoicesAsync(Cancellation);
    }

    private static AuditEntry Entry(Guid organization, string actor, string action, DateTimeOffset at) => new()
    {
        OrganizationId = organization,
        ActorEmail = actor,
        Action = action,
        SubjectType = "Membership",
        SubjectName = "a member",
        At = at,
    };
}
