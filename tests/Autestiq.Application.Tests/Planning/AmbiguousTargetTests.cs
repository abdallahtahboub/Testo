using Autestiq.Ai;
using Autestiq.Application.Planning;
using Autestiq.Contracts.Planning;

namespace Autestiq.Application.Tests.Planning;

/// <summary>
/// A step that names an element the crawl could not tell apart is refused (ADR-0024 in software). The prompt already
/// asked for this; on eShopOnWeb a plan ignored it once, checked a pager text shown twice, and failed a sound shop.
/// </summary>
public class AmbiguousTargetTests
{
    private const string Pager = "Showing 2 of 12 products - Page 2 - 2";

    /// <summary>eShopOnWeb's second catalogue page, as the explorer describes it: the pager twice, use=none.</summary>
    private static readonly SiteSnapshot Catalogue = new(
        "http://127.0.0.1:5106",
        [
            new PageSnapshot("/?pageId=1", "Catalog", null,
            [
                new SnapshotElement("text", Pager, Text: Pager),
                new SnapshotElement("text", Pager, Text: Pager),
                new SnapshotElement("text", "Prism White TShirt", Text: "Prism White TShirt", NamedBy: TargetKind.Text),
                new SnapshotElement("button", "[ ADD TO BASKET ]", Within: "Prism White TShirt", NamedBy: TargetKind.RoleAndName),
                new SnapshotElement("button", "[ ADD TO BASKET ]", Within: "Cup<T> Sheet", NamedBy: TargetKind.RoleAndName),
            ]),
        ],
        Truncated: false);

    private static TestPlan Plan(params PlannedAction[] checks) =>
        new("Second page", [new PlannedAction(ActionKind.Goto, "Open the second page", Path: "/?pageId=1"), .. checks], []);

    private static PlannedAction See(ActionTarget target) => new(ActionKind.AssertVisible, "It is there", target);

    [Fact]
    public void Review_ACheckOfTextTheCrawlMarkedUseNone_IsRefused()
    {
        // The step that failed eShopOnWeb's sound second page.
        var review = PlanValidator.Review(Plan(See(ActionTarget.ByText(Pager)), See(ActionTarget.ByText("Prism White TShirt"))), Catalogue, maxActions: 20);

        Assert.Equal([PlanningErrors.AmbiguousTarget], review.Errors);
    }

    [Fact]
    public void Review_AnElementNamedWithoutTheWithinItNeeds_IsRefused_AndWithIt_IsAllowed()
    {
        var without = PlanValidator.Review(Plan(See(ActionTarget.ByRole("button", "[ ADD TO BASKET ]"))), Catalogue, maxActions: 20);
        var with = PlanValidator.Review(
            Plan(See(ActionTarget.ByRole("button", "[ ADD TO BASKET ]").InsideOf("Cup<T> Sheet"))), Catalogue, maxActions: 20);

        Assert.Equal([PlanningErrors.AmbiguousTarget], without.Errors);
        Assert.True(with.Allowed);
    }

    [Fact]
    public void Review_AnElementTheCrawlNamedUniquely_IsAllowed() =>
        Assert.True(PlanValidator.Review(Plan(See(ActionTarget.ByText("Prism White TShirt"))), Catalogue, maxActions: 20).Allowed);

    [Fact]
    public void Review_SomethingTheCrawlNeverSaw_IsNotJudged_ButStillNoted()
    {
        // It may appear only once the test acts: the usual case for a confirmation.
        var review = PlanValidator.Review(Plan(See(ActionTarget.ByText("Thanks for your Order!"))), Catalogue, maxActions: 20);

        Assert.True(review.Allowed);
        Assert.Single(review.Notes);
    }

    [Fact]
    public void Review_AnElementNamedAnotherWayThanTheCrawlChose_IsNotRefused()
    {
        // The crawl stopped at the first way that worked; another way may be unique too, and only the run can say.
        var review = PlanValidator.Review(Plan(See(ActionTarget.ByRole("text", "Prism White TShirt"))), Catalogue, maxActions: 20);

        Assert.DoesNotContain(PlanningErrors.AmbiguousTarget, review.Errors);
    }

    [Fact]
    public void Review_ADescriptionThatSaysHowToNameNothing_GivesTheRuleNothingToGoOn()
    {
        // Made before ADR-0024, or by hand: every element without a NamedBy, which here means "not recorded".
        var old = new SiteSnapshot("http://127.0.0.1:5090", [new PageSnapshot("/", "Catalogue", "Catalogue", [new SnapshotElement("text", Pager, Text: Pager)])], false);

        Assert.True(PlanValidator.Review(Plan(See(ActionTarget.ByText(Pager))), old, maxActions: 20).Allowed);
    }

    [Fact]
    public void Review_ANegativeCheckOfAnAmbiguousElement_IsRefusedToo()
    {
        // "Not visible" of something shown twice passes or fails on which copy the browser picks: no verdict either way.
        var review = PlanValidator.Review(
            Plan(new PlannedAction(ActionKind.AssertNotVisible, "No pager", ActionTarget.ByText(Pager))), Catalogue, maxActions: 20);

        Assert.Equal([PlanningErrors.AmbiguousTarget], review.Errors);
    }

    [Fact]
    public void Retry_TellsThePlannerWhatWasWrong_InWordsItCanActOn()
    {
        var request = PlanRequest.First("Check page 2", Catalogue, [], 20, 40_000).Retry([PlanningErrors.AmbiguousTarget]);

        var prompt = PlanPrompt.User(request);

        Assert.Contains("use=none", prompt, StringComparison.Ordinal);
        Assert.Contains("more than one element", prompt, StringComparison.Ordinal);
    }
}
