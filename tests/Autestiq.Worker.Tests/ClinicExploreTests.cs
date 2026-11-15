using Autestiq.Contracts.Execution;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// Exploring the booking clinic: an application with a page for every date, a form that checks what it
/// is given, and a diary kept on the server.
/// </summary>
/// <remarks>
/// One class, so that its tests run one after another: the diary belongs to the whole process, and two
/// explorations at once would empty it under each other.
/// </remarks>
public class ClinicExploreTests(ClinicFixture clinic) : IClassFixture<ClinicFixture>
{
    private const string InitialDetails = "/book/initial/details?date=2026-11-16&time=09:00";

    private Task<WorkerResult> ReadAsync() =>
        Explorations.OnceAsync(clinic.Origin, pressing: false, forms: false);

    private Task<WorkerResult> PressedAsync() =>
        Explorations.OnceAsync(clinic.Origin, pressing: true, forms: false, ClinicFixture.StartAfreshAsync);

    private Task<WorkerResult> FilledAsync() =>
        Explorations.OnceAsync(clinic.Origin, pressing: true, forms: true, ClinicFixture.StartAfreshAsync);

    [Fact]
    public async Task Crawl_DescribesOnePageOfEachKind_AndDoesNotSayItStoppedShort()
    {
        // Five dates for each of four services, and five times for each date: a hundred and twenty
        // addresses, and three kinds of page. The first measurement spent the budget on three dates of
        // one service, never reached the booking form, and told the planner it had stopped short, which
        // the planner then repeated in every plan it wrote.
        var snapshot = Explorations.SnapshotOf(await ReadAsync());

        Assert.Single(snapshot.Pages, page => page.Path.StartsWith("/book/initial/time", StringComparison.Ordinal));
        Assert.Single(snapshot.Pages, page => page.Path.StartsWith("/book/initial/details", StringComparison.Ordinal));
        Assert.Contains(snapshot.Pages, page => page.Path == InitialDetails);

        Assert.Equal(17, snapshot.Pages.Count);
        Assert.False(snapshot.Truncated, "every kind of page was described, so nothing was left out.");
    }

    [Fact]
    public async Task Crawl_WithABudgetSmallerThanTheKindsOfPage_SaysItStoppedShort()
    {
        // The other half of the same rule. Seventeen kinds do not fit in twelve pages, and saying so is
        // the difference between a part and a whole.
        var result = await Jobs.ExploreAsync(clinic.Origin, RunBudget.Default);
        var snapshot = Explorations.SnapshotOf(result);

        Assert.Equal(RunBudget.Default.MaxPagesCrawled, snapshot.Pages.Count);
        Assert.True(snapshot.Truncated);
    }

    [Fact]
    public async Task Forms_ReachTheBookingConfirmation()
    {
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        var confirmation = snapshot.Pages.FirstOrDefault(page => page.Elements.Any(element => element.TestId == "booking-ref"));

        Assert.NotNull(confirmation);
        Assert.Equal(["open " + InitialDetails, "type test values, then click confirm-booking"], confirmation.ReachedBy);
        Assert.Contains(confirmation.Elements, element => element.TestId == "booking-status" && element.Name == "Confirmed");
    }

    [Fact]
    public async Task Forms_ReachWhatTheFormSaysAboutAMissingName()
    {
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        var refused = snapshot.Pages.FirstOrDefault(page => page.Elements.Any(element => element.TestId == "error-name"));

        Assert.NotNull(refused);
        Assert.Equal(["open " + InitialDetails, "click confirm-booking"], refused.ReachedBy);
    }

    [Fact]
    public async Task Forms_ReachWhatTheFormSaysAboutAnAddressThatIsNotOne()
    {
        // The clinic asks for an email address in a plain text field and checks it itself. A value
        // chosen by the field's type alone never gets a name past it, and never shows this message.
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        var refused = snapshot.Pages.FirstOrDefault(page => page.Elements.Any(element => element.TestId == "error-email"));

        Assert.NotNull(refused);
        Assert.Equal(
            ["open " + InitialDetails, "type test values with an email address that is not one, then click confirm-booking"],
            refused.ReachedBy);
    }

    [Fact]
    public async Task Forms_ReachTheLookUpThatFindsNothing()
    {
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        Assert.True(Explorations.Shows(snapshot, "find-error"));
    }

    [Fact]
    public async Task Pressing_NeverCancelsABooking()
    {
        foreach (var result in new[] { await PressedAsync(), await FilledAsync() })
        {
            Assert.False(Explorations.Pressed(Explorations.SnapshotOf(result), "cancel-booking"));
        }

        // Described, so that a plan can press it. Not pressed here.
        Assert.True(Explorations.Shows(Explorations.SnapshotOf(await FilledAsync()), "cancel-booking"));
    }

    [Fact]
    public async Task EveryVisit_StartsFromAnEmptyDiary()
    {
        // The clinic books the same slot for every service. If a visit inherited the last one's booking,
        // the second booking would be refused, and the snapshot would say that booking a follow-up leads
        // to "that time has just been booked", which is false on the fresh clinic a plan runs against.
        var snapshot = Explorations.SnapshotOf(await FilledAsync());

        var references = Explorations.Elements(snapshot)
            .Where(element => element.TestId == "booking-ref")
            .Select(element => element.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["BK-1001"], references);
        Assert.False(Explorations.Shows(snapshot, "error-slot"));
    }

    [Fact]
    public async Task Pressing_WithoutForms_MakesNoBooking()
    {
        // Without the second permission, a control named for sending a form is not pressed at all.
        var snapshot = Explorations.SnapshotOf(await PressedAsync());

        Assert.False(Explorations.Pressed(snapshot, "confirm-booking"));
        Assert.False(Explorations.Pressed(snapshot, "find-submit"));
        Assert.False(Explorations.Shows(snapshot, "booking-ref"));
    }

    [Fact]
    public async Task AnApplicationAddressedByName_IsReadAndNotPressed()
    {
        // The whole rule, end to end. The same clinic, asked for in the same way, but addressed as
        // "localhost": a name, which could be anybody's. It is read, and nothing on it is pressed, and
        // the run says so.
        var result = await Explorations.OnceAsync(
            clinic.Origin,
            pressing: true,
            forms: true,
            ClinicFixture.StartAfreshAsync,
            named: clinic.OriginByName);

        var snapshot = Explorations.SnapshotOf(result);

        Assert.NotEmpty(snapshot.Pages);
        Assert.DoesNotContain(snapshot.Pages, page => page.ReachedBy is { Count: > 0 });
        Assert.Contains("refused", result.Steps[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pressing_StaysWithinAHundredVisits()
    {
        var visits = Explorations.Visits(await FilledAsync());

        Assert.InRange(visits, 1, 100);
    }
}
