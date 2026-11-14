using Testo.Contracts.Execution;
using Testo.Contracts.Planning;
using Testo.DemoShop;
using Testo.Worker.Tests.Support;

namespace Testo.Worker.Tests;

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
}
