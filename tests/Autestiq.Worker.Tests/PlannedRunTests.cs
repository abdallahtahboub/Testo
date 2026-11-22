using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.DemoShop;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// A plan carried out by the worker. The plans here are written by hand rather than by a model,
/// because what is under test is the execution of a plan, not the making of one — and because a
/// test whose input comes from a model is not a test.
/// </summary>
[Collection(Serially.Name)]
public class PlannedRunTests(DemoShopFixture shop)
{
    [Fact]
    public async Task APlanThatHolds_PassesAndKeepsEvidenceOfEveryStep()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.AssertText, "The catalogue is the catalogue",
                ActionTarget.ByRole("heading", "Catalogue"), Text: "Catalogue"),
            new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")),
            new(ActionKind.AssertVisible, "The teapot is in the basket", ActionTarget.ByTestId("line-pot")),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan, out var artifacts);

        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.Equal(4, result.Steps.Count);
        Assert.All(result.Steps, step => Assert.Equal(StepStatus.Passed, step.Status));

        // The same evidence a scripted run keeps. A planned run is not a lesser kind of run.
        Assert.All(result.Steps, step =>
            Assert.True(File.Exists(Path.Combine(artifacts, step.ScreenshotFile!)), $"step {step.Order} left no screenshot."));
    }

    [Fact]
    public async Task AReadOnlyRun_ThatPressesAButtonWhichPostsAForm_SendsNothing_AndHasNoVerdict()
    {
        // "Add the teapot" posts a form. Approved as read-only, the post never leaves the browser, so the basket stays
        // empty, and the failure that follows is said to be ours, not the application's.
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")),
            new(ActionKind.AssertVisible, "The teapot is in the basket", ActionTarget.ByTestId("line-pot")),
        ];

        var result = await Jobs.ReadOnlyAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Inconclusive, result.Status);
        Assert.Contains("POST /basket/add", result.FailureReason!, StringComparison.Ordinal);
        Assert.Contains("was blocked", result.FailureReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReadOnlyRun_ThatOnlyReads_PassesAsAnyRunWould()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.AssertText, "The catalogue is the catalogue", ActionTarget.ByRole("heading", "Catalogue"), Text: "Catalogue"),
        ];

        var result = await Jobs.ReadOnlyAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task APlanWhoseAssertionIsWrongAboutTheApplication_Fails_AndNamesTheStep()
    {
        // The known defect, found by a plan instead of by a scripted scenario: the basket says
        // £53.25 and the checkout asks for £42.00. Switched on for this test only.
        using var defect = DemoShopDefects.On(Defects.CheckoutTotal);

        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")),
            new(ActionKind.Goto, "Back to the catalogue", Path: "/"),
            new(ActionKind.Click, "Add the cup", ActionTarget.ByTestId("add-cup")),
            new(ActionKind.Goto, "Open the checkout", Path: "/checkout"),
            new(ActionKind.AssertText, "The checkout charges the basket total",
                ActionTarget.ByTestId("checkout-total"), Text: "£53.25"),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Failed, result.Status);

        var failed = Assert.Single(result.Steps, step => step.Status == StepStatus.Failed);
        Assert.Equal("The checkout charges the basket total", failed.Description);
        Assert.Contains("£53.25", failed.Message!, StringComparison.Ordinal);

        // A defect in the application under test, not an infrastructure problem. The distinction
        // the whole status list exists for.
        Assert.NotEqual(RunStatus.InfrastructureError, result.Status);
    }

    [Fact]
    public async Task APlanThatChecksAnAbsence_PassesWhenTheThingIsGone()
    {
        // The action the first measured benchmark run asked for three times: half of what a person
        // wants checked is that something is no longer there (ADR-0019 §2).
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")),
            new(ActionKind.AssertVisible, "The teapot is in the basket", ActionTarget.ByTestId("line-pot")),
            new(ActionKind.Click, "Take it out again", ActionTarget.ByTestId("remove-pot")),
            new(ActionKind.AssertNotVisible, "The teapot is gone", ActionTarget.ByTestId("line-pot")),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.All(result.Steps, step => Assert.Equal(StepStatus.Passed, step.Status));
    }

    [Fact]
    public async Task APlanThatChecksAnAbsence_FailsWhenTheThingIsStillThere()
    {
        // The mistake this action exists to catch: a removal that does not remove. Without it the
        // only way to express this is to assert some other presence, which passes either way.
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")),
            new(ActionKind.AssertNotVisible, "The teapot is gone", ActionTarget.ByTestId("line-pot")),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        var failed = Assert.Single(result.Steps, step => step.Status == StepStatus.Failed);
        Assert.Equal("The teapot is gone", failed.Description);
        Assert.NotEqual(RunStatus.InfrastructureError, result.Status);
    }

    [Fact]
    public async Task APlanThatTypesASecret_UsesTheValueWithoutEverCarryingIt()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.AssertVisible, "There is something to search with", ActionTarget.ByTestId("search")),
            new(ActionKind.Type, "Type the stored term", ActionTarget.ByTestId("search"), SecretName: "term"),
            new(ActionKind.Press, "Search", ActionTarget.ByTestId("search"), Text: "Enter"),
            new(ActionKind.AssertVisible, "The teapot was found", ActionTarget.ByTestId("name-pot")),
        ];

        var result = await Jobs.PlannedAsync(
            shop.Origin,
            plan,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["term"] = "teapot" });

        Assert.Equal(RunStatus.Passed, result.Status);

        // The plan said "term" and nothing in the plan or the result says "teapot". A stored plan
        // is therefore not a secret (ADR-0019 §4).
        Assert.DoesNotContain("teapot", string.Join("\n", plan.Select(action => $"{action.Text}{action.SecretName}")), StringComparison.Ordinal);
        Assert.All(result.Steps, step => Assert.DoesNotContain("teapot", step.Message ?? "", StringComparison.Ordinal));
    }

    /// <summary>What a tester of the demo shop would say an order ends with (ADR-0034).</summary>
    private static readonly ExpectedResult OrderPlaced = new("Thank you. Your order is on its way.", "/order/placed");

    /// <summary>Places an order, then checks the tester's stated result, as a plan written from it would.</summary>
    private static PlannedAction[] OrderThenStatedChecks(bool addSomething) =>
    [
        new(ActionKind.Goto, "Open the catalogue", Path: "/"),
        .. addSomething ? new PlannedAction[] { new(ActionKind.Click, "Add the teapot", ActionTarget.ByTestId("add-pot")) } : [],
        new(ActionKind.Goto, "Open the checkout", Path: "/checkout"),
        new(ActionKind.Click, "Place the order", ActionTarget.ByTestId("place-order")),
        new(ActionKind.AssertVisible, "The shop says the order is on its way", ActionTarget.ByText(OrderPlaced.Text)),
        new(ActionKind.AssertUrl, "On the confirmation", Path: OrderPlaced.Path),
    ];

    [Fact]
    public async Task ChecksOfAStatedResult_PassWhenTheApplicationShowsIt()
    {
        var plan = OrderThenStatedChecks(addSomething: true);

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);

        // Labelled as the tester's: exploring never placed an order, so nothing observed it.
        Assert.Equal(
            [CheckEvidence.StatedByTester, CheckEvidence.StatedByTester],
            ExpectedResults.EvidenceFor(plan, SiteSnapshot.Empty(shop.Origin), [OrderPlaced]).Where(evidence => evidence is not null));
    }

    [Fact]
    public async Task ChecksOfAStatedResult_FailWhenTheApplicationDoesNotShowIt()
    {
        // An empty basket: the sound shop refuses the order, so the thanks never appear.
        var result = await Jobs.PlannedAsync(shop.Origin, OrderThenStatedChecks(addSomething: false));

        Assert.Equal(RunStatus.Failed, result.Status);
    }

    /// <summary>The shop's one account, as an environment's stored test account hands it to a run (ADR-0033).</summary>
    private static Dictionary<string, string> Account(string password = Shop.Password) => new(StringComparer.Ordinal)
    {
        ["username"] = Shop.Account,
        ["password"] = password,
    };

    /// <summary>Signs in with the stored account, then makes one check.</summary>
    private static PlannedAction[] SignInThen(PlannedAction check, bool typeUsernamePlainly = false) =>
    [
        new(ActionKind.Goto, "Open the sign-in page", Path: "/sign-in"),
        typeUsernamePlainly
            ? new(ActionKind.Type, "Type the email", ActionTarget.ByTestId("email"), Text: Shop.Account)
            : new(ActionKind.Type, "Type the email", ActionTarget.ByTestId("email"), SecretName: "username"),
        new(ActionKind.Type, "Type the password", ActionTarget.ByTestId("password"), SecretName: "password"),
        new(ActionKind.Click, "Sign in", ActionTarget.ByTestId("do-sign-in")),
        check,
    ];

    private static PlannedAction ShowsTheAccountsEmail =>
        new(ActionKind.AssertText, "It says who is signed in", ActionTarget.ByTestId("signed-in-as"), Text: $"Signed in as {Shop.Account}");

    [Fact]
    public async Task APassThatRestsOnTheTestAccountsOwnValue_IsInconclusive_SayingWhichByName()
    {
        // The application could show the value the test typed whether or not the sign-in worked: eShopOnWeb prints
        // its demo account on its login page. Such a pass proves nothing, so it is not counted as one.
        var result = await Jobs.PlannedAsync(shop.Origin, SignInThen(ShowsTheAccountsEmail), Account());

        Assert.Equal(RunStatus.Inconclusive, result.Status);
        Assert.Contains("'username'", result.FailureReason!, StringComparison.Ordinal);
        Assert.DoesNotContain(Shop.Account, result.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Shop.Password, result.FailureReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APassThatRestsOnTheAccountsValueTypedPlainly_IsInconclusive_Too()
    {
        // A planner that read the value in the tester's description may type it as text. It is the same value.
        var result = await Jobs.PlannedAsync(shop.Origin, SignInThen(ShowsTheAccountsEmail, typeUsernamePlainly: true), Account());

        Assert.Equal(RunStatus.Inconclusive, result.Status);
    }

    [Fact]
    public async Task ASignInCheckedBySomethingOnlyTheSignedInPageShows_Passes()
    {
        // The guard takes away nothing but that one kind of pass.
        var check = new PlannedAction(ActionKind.AssertVisible, "The account page is shown", ActionTarget.ByTestId("signed-in-as"));

        var result = await Jobs.PlannedAsync(shop.Origin, SignInThen(check), Account());

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task ACheckForTheAccountsValueThatFails_StillFails()
    {
        // A failure has found something; only a pass is in question.
        var result = await Jobs.PlannedAsync(shop.Origin, SignInThen(ShowsTheAccountsEmail), Account(password: "not-the-password"));

        Assert.Equal(RunStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ACheckThatTheAccountsValueIsNotThere_IsNotAffected()
    {
        // "Not there" cannot pass because the test typed the value, so it is left alone.
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the sign-in page", Path: "/sign-in"),
            new(ActionKind.Type, "Type the email", ActionTarget.ByTestId("email"), SecretName: "username"),
            new(ActionKind.AssertNotVisible, "Nobody is signed in yet", ActionTarget.ByText($"Signed in as {Shop.Account}")),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan, Account());

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task APlanThatTypesASecretTheRunDoesNotHave_FailsSayingWhichOne()
    {
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Type, "Type something nobody gave us",
                ActionTarget.ByTestId("search"), SecretName: "password"),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        var failed = Assert.Single(result.Steps, step => step.Status == StepStatus.Failed);

        // The name is safe to say. It is the value that never appears anywhere.
        Assert.Contains("password", failed.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlanNamingATargetByItsRoleAndName_FindsIt()
    {
        // The other half of the vocabulary: an application with no test ids is still testable by
        // what a person would call its parts.
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Follow the basket link", ActionTarget.ByRole("link", "Basket")),
            new(ActionKind.AssertUrl, "We are on the basket", Path: "/basket"),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task AJobThatClaimsAPlanAndCarriesNone_IsAnInfrastructureError()
    {
        // Our fault, not the application's, and the status says so.
        var result = await Jobs.PlannedAsync(shop.Origin, []);

        Assert.Equal(RunStatus.InfrastructureError, result.Status);
        Assert.Contains("carries none", result.FailureReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlanThatNamesElementsByWhatTheySay_Passes()
    {
        // ADR-0024. The demo shop has test ids, and this plan does not use one: four buttons all say "Add to
        // basket", so the teapot's is named by the item it is in, and the result by its text.
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Add the teapot", ActionTarget.ByRole("button", "Add to basket").InsideOf("Cast iron teapot")),
            new(ActionKind.AssertUrl, "The basket opens", Path: "/basket"),
            new(ActionKind.AssertVisible, "The teapot is in it", ActionTarget.ByText("Cast iron teapot")),
            new(ActionKind.AssertNotVisible, "The cup is not", ActionTarget.ByText("Porcelain cup")),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task APlanThatNamesARepeatWithoutSayingWhichOne_Fails_RatherThanPickingOne()
    {
        // Four buttons say "Add to basket". Without "within", the target is ambiguous, and an ambiguous
        // target is a failed step, never a guess.
        PlannedAction[] plan =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.Click, "Add something", ActionTarget.ByRole("button", "Add to basket")),
            new(ActionKind.AssertUrl, "The basket opens", Path: "/basket"),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, plan);

        Assert.Equal(RunStatus.Failed, result.Status);
    }
}
