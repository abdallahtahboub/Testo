using Microsoft.Extensions.Logging.Abstractions;
using Testo.Application.Planning;
using Testo.Contracts.Execution;
using Testo.Contracts.Planning;

namespace Testo.Application.Tests.Planning;

/// <summary>
/// The only place the application speaks to a model, so the only place where "what if it answers
/// badly" has to be settled. The planner here is a fake that answers whatever the test wants.
/// </summary>
public class PlanningServiceTests
{
    private static readonly SiteSnapshot Shop = new(
        "http://127.0.0.1:5090",
        [
            new PageSnapshot("/", "Catalogue", "Catalogue",
                [new SnapshotElement("button", "Add to basket", "add-pot")]),
        ],
        Truncated: false);

    private static readonly PlannedAction[] Good =
    [
        new(ActionKind.Goto, "Open the catalogue", Path: "/"),
        new(ActionKind.AssertVisible, "The teapot can be added", ActionTarget.ByTestId("add-pot")),
    ];

    private static PlanningService Service(Planner planner) => new(planner, NullLogger<PlanningService>.Instance);

    [Fact]
    public async Task Plan_WhenTheFirstAnswerIsGood_AsksOnceAndCharges()
    {
        var planner = new Planner([Answer(Good)]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue", Shop, RunBudget.Default, [], TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, planner.Asked);
        Assert.Equal(1_200, result.Cost.TotalTokens);
    }

    [Fact]
    public async Task Plan_WhenTheFirstAnswerIsRefused_AsksAgainWithTheErrors()
    {
        // A model given its own mistake usually fixes it, which is worth exactly one more call.
        var planner = new Planner([Answer([new PlannedAction(ActionKind.Goto, "Go abroad", Path: "https://collect.example/")]), Answer(Good)]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue", Shop, RunBudget.Default, [], TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, planner.Asked);

        // It was told what was wrong, rather than asked the same question twice.
        Assert.Contains(PlanningErrors.ForeignOrigin, planner.LastRequest!.PreviousErrors);

        // And both attempts are paid for.
        Assert.Equal(2_400, result.Cost.TotalTokens);
    }

    [Fact]
    public async Task Plan_WhenTheSecondAnswerIsAlsoRefused_GivesUpWithoutRunningAnything()
    {
        var bad = Answer([new PlannedAction(ActionKind.Goto, "Go abroad", Path: "https://collect.example/")]);
        var planner = new Planner([bad, bad]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue", Shop, RunBudget.Default, [], TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Plan);
        Assert.Contains(PlanningErrors.ForeignOrigin, result.Errors);

        // Twice and no more: an argument with a model is not a strategy, and each round is money.
        Assert.Equal(PlanningService.MaxAttempts, planner.Asked);
    }

    [Fact]
    public async Task Plan_NeverReturnsThePartOfABadPlanThatWouldHaveWorked()
    {
        // The temptation this guards against: the first two actions of that plan are fine, and
        // running them would produce a run that reports steps for a test nobody approved.
        var planner = new Planner(
        [
            Answer(
            [
                new PlannedAction(ActionKind.Goto, "Open the catalogue", Path: "/"),
                new PlannedAction(ActionKind.AssertVisible, "The teapot", ActionTarget.ByTestId("add-pot")),
                new PlannedAction(ActionKind.Goto, "Then leave", Path: "https://collect.example/"),
            ]),
        ]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue", Shop, RunBudget.Default, [], TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Plan);
    }

    [Fact]
    public async Task Plan_WhenTheProviderCannotBeReached_StopsRatherThanRetrying()
    {
        // Nothing to re-ask about: a second call to a provider that is down costs time and tells
        // us the same thing.
        var planner = new Planner([PlanResult.Failed([PlanningErrors.ProviderUnavailable], new PlanningCost(0, 0, 1, 0m, TimeSpan.Zero), "fake")]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue", Shop, RunBudget.Default, [], TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(PlanningErrors.ProviderUnavailable, result.Errors);
        Assert.Equal(1, planner.Asked);
    }

    [Fact]
    public async Task Plan_WhenTheFirstAttemptSpendsTheTokenBudget_DoesNotAskAgain()
    {
        var expensive = PlanResult.From(
            new TestPlan("A plan", [new PlannedAction(ActionKind.Goto, "Go abroad", Path: "https://collect.example/")], []),
            new PlanningCost(9_000, 1_000, 1, 0.02m, TimeSpan.FromSeconds(3)),
            "fake");

        var planner = new Planner([expensive, Answer(Good)]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue",
            Shop,
            RunBudget.Default with { MaxModelTokens = 5_000 },
            [],
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(PlanningErrors.BudgetSpent, result.Errors);
        Assert.Equal(1, planner.Asked);
    }

    [Fact]
    public async Task Plan_WhenAGoodPlanCostMoreThanTheMoneyBudget_IsNotRun()
    {
        // The plan is fine and we still cannot afford it. Reported as the budget rather than as a
        // fault in the plan, because that is what happened.
        var planner = new Planner([PlanResult.From(new TestPlan("A plan", Good, []), new PlanningCost(100, 100, 1, 5.00m, TimeSpan.FromSeconds(2)), "fake")]);

        var result = await Service(planner).PlanAsync(
            "Check the catalogue",
            Shop,
            RunBudget.Default with { MaxCostUsd = 0.25m },
            [],
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(PlanningErrors.BudgetSpent, result.Errors);
    }

    [Fact]
    public async Task Plan_KeepsWhatTheModelWantedAPersonToKnowAndWhatTheValidatorNoticed()
    {
        var planner = new Planner(
        [
            PlanResult.From(
                new TestPlan(
                    "A plan",
                    [
                        new PlannedAction(ActionKind.Goto, "Open the catalogue", Path: "/"),
                        new PlannedAction(ActionKind.AssertVisible, "The line is there", ActionTarget.ByTestId("line-pot")),
                    ],
                    ["I assumed prices are in pounds."]),
                new PlanningCost(700, 500, 1, 0.01m, TimeSpan.FromSeconds(2)),
                "fake"),
        ]);

        var result = await Service(planner).PlanAsync(
            "Check the basket", Shop, RunBudget.Default, [], TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Notes, note => note.Contains("in pounds", StringComparison.Ordinal));
        Assert.Contains(result.Notes, note => note.Contains("line-pot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Plan_TellsThePlannerOnlyTheNamesOfTheRunsSecrets()
    {
        var planner = new Planner([Answer(Good)]);

        await Service(planner).PlanAsync(
            "Sign in and check the basket",
            Shop,
            RunBudget.Default,
            ["password"],
            TestContext.Current.CancellationToken);

        Assert.Equal(["password"], planner.LastRequest!.SecretNames);
    }

    private static PlanResult Answer(IReadOnlyList<PlannedAction> actions) => PlanResult.From(
        new TestPlan("A plan", actions, []),
        new PlanningCost(900, 300, 1, 0.01m, TimeSpan.FromSeconds(2)),
        "fake");

    /// <summary>A planner that answers from a list, in order, and remembers what it was asked.</summary>
    private sealed class Planner(IReadOnlyList<PlanResult> answers) : IScenarioPlanner
    {
        public string Model => "fake";

        public int Asked { get; private set; }

        public PlanRequest? LastRequest { get; private set; }

        public Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var answer = answers[Math.Min(Asked, answers.Count - 1)];
            Asked++;
            return Task.FromResult(answer);
        }
    }
}
