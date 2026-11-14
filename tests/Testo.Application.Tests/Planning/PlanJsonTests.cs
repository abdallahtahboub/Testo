using Testo.Ai;
using Testo.Contracts.Planning;

namespace Testo.Application.Tests.Planning;

/// <summary>
/// Reading a model's answer. The one place an untyped string from a provider becomes something the
/// product acts on, so it is strict, and the tests are mostly about what it refuses.
/// </summary>
public class PlanJsonTests
{
    [Fact]
    public void Read_AnOrdinaryAnswer_BecomesAPlan()
    {
        var plan = PlanJson.Read(
            """
            {
              "scenarioName": "Basket keeps its items",
              "notes": ["I assumed prices are in pounds."],
              "actions": [
                { "kind": "goto", "description": "Open the catalogue", "path": "/" },
                { "kind": "click", "description": "Add the teapot", "target": { "kind": "testId", "value": "add-pot" } },
                { "kind": "assertText", "description": "The total is right", "target": { "kind": "testId", "value": "basket-total" }, "text": "£53.25" }
              ]
            }
            """);

        Assert.NotNull(plan);
        Assert.Equal("Basket keeps its items", plan.ScenarioName);
        Assert.Equal(3, plan.Actions.Count);
        Assert.Equal(1, plan.AssertionCount);
        Assert.Equal(["I assumed prices are in pounds."], plan.Notes);

        Assert.Equal(ActionKind.Click, plan.Actions[1].Kind);
        Assert.Equal(TargetKind.TestId, plan.Actions[1].Target!.Kind);
        Assert.Equal("add-pot", plan.Actions[1].Target!.Value);
    }

    [Fact]
    public void Read_AnAnswerWrappedInProseAndAFencedBlock_StillBecomesAPlan()
    {
        // Models do this however firmly they are asked not to. Leniency about the packaging is
        // not leniency about the content: what is inside is still read strictly.
        var plan = PlanJson.Read(
            """
            Certainly! Here is the plan:

            ```json
            {
              "scenarioName": "A plan",
              "actions": [
                { "kind": "assertUrl", "description": "We are on the basket", "path": "/basket" }
              ]
            }
            ```

            Let me know if you would like me to change anything.
            """);

        Assert.NotNull(plan);
        Assert.Equal(ActionKind.AssertUrl, Assert.Single(plan.Actions).Kind);
    }

    [Fact]
    public void Read_AnActionNobodyDefined_MakesTheWholeAnswerUnreadable()
    {
        // Not "skip that one". Dropping an action changes what the plan does, and nobody asked us
        // to edit it (ADR-0019 §3).
        var plan = PlanJson.Read(
            """
            {
              "actions": [
                { "kind": "goto", "description": "Open the catalogue", "path": "/" },
                { "kind": "evaluate", "description": "Just a little script", "text": "fetch('https://collect.example')" }
              ]
            }
            """);

        Assert.Null(plan);
    }

    [Theory]
    [InlineData("script")]
    [InlineData("evaluate")]
    [InlineData("execute")]
    [InlineData("runJs")]
    [InlineData("navigate")]
    [InlineData("screenshot")]
    public void Read_AnyActionOutsideTheVocabulary_IsRefused(string kind)
    {
        var plan = PlanJson.Read($$"""
            { "actions": [ { "kind": "{{kind}}", "description": "Something", "text": "anything" } ] }
            """);

        Assert.Null(plan);
    }

    [Fact]
    public void Read_ASelectorSmuggledIntoAFieldNobodyDefined_IsIgnoredRatherThanHonoured()
    {
        // The field does not exist on the type, so it goes nowhere. Worth a test because "it is
        // not in the type" is the entire defence, and a defence worth having is worth checking.
        var plan = PlanJson.Read(
            """
            {
              "actions": [
                {
                  "kind": "click",
                  "description": "Add the teapot",
                  "target": { "kind": "testId", "value": "add-pot" },
                  "selector": "button:has(.pay) >> nth=0",
                  "script": "window.location='https://collect.example'"
                }
              ]
            }
            """);

        Assert.NotNull(plan);

        var action = Assert.Single(plan.Actions);
        Assert.Equal("add-pot", action.Target!.Value);

        // Nothing carried the smuggled values: there is no property they could have landed in.
        Assert.Null(action.Text);
        Assert.Null(action.Path);
        Assert.Null(action.SecretName);
    }

    [Fact]
    public void Read_ATargetShapeNobodyDefined_IsRefused()
    {
        Assert.Null(PlanJson.Read(
            """
            { "actions": [ { "kind": "click", "description": "Click it", "target": { "kind": "css", "value": ".pay" } } ] }
            """));
    }

    [Fact]
    public void Read_ARoleTargetWithoutARole_IsRefused()
    {
        Assert.Null(PlanJson.Read(
            """
            { "actions": [ { "kind": "click", "description": "Click it", "target": { "kind": "roleAndName", "value": "Pay" } } ] }
            """));
    }

    [Fact]
    public void Read_AnActionWithNoDescription_IsRefused()
    {
        // A step nobody can read is a step nobody can review, and the run page would show a blank.
        Assert.Null(PlanJson.Read(
            """
            { "actions": [ { "kind": "goto", "path": "/" } ] }
            """));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I am afraid I cannot help with that.")]
    [InlineData("{ not json at all")]
    [InlineData("{}")]
    [InlineData("""{ "actions": [] }""")]
    public void Read_AnythingThatIsNotAPlan_IsNull(string answer)
    {
        Assert.Null(PlanJson.Read(answer));
    }

    [Theory]
    [InlineData("\"coverage\": \"partial\",", PlanCoverage.Partial)]
    [InlineData("\"coverage\": \"full\",", PlanCoverage.Full)]
    [InlineData("", PlanCoverage.Full)]
    [InlineData("\"coverage\": \"mostly, see notes\",", PlanCoverage.Partial)]
    public void Read_Coverage_IsPartialUnlessTheAnswerSaysOtherwise(string line, PlanCoverage expected)
    {
        // An answer that says nothing is read as a full plan, which is the lenient reading of
        // silence. Anything that is not a word we recognize is read as partial: being unsure that
        // a scenario was covered is the same as not having covered it.
        var plan = PlanJson.Read(
            $$"""
            {
              {{line}}
              "actions": [
                { "kind": "assertUrl", "description": "We got there", "path": "/basket" }
              ]
            }
            """);

        Assert.Equal(expected, plan!.Coverage);
    }

    [Fact]
    public void Read_AnAbsenceAssertion_IsUnderstood()
    {
        var plan = PlanJson.Read(
            """
            {
              "actions": [
                { "kind": "assertNotVisible", "description": "The cup is gone", "target": { "kind": "testId", "value": "line-cup" } }
              ]
            }
            """);

        var action = Assert.Single(plan!.Actions);

        Assert.Equal(ActionKind.AssertNotVisible, action.Kind);
        Assert.True(action.IsAssertion);
    }

    [Fact]
    public void Read_ASecretByName_KeepsTheNameAndNoValue()
    {
        var plan = PlanJson.Read(
            """
            {
              "actions": [
                { "kind": "type", "description": "Type the password", "target": { "kind": "testId", "value": "password" }, "secretName": "password" },
                { "kind": "assertVisible", "description": "We are in", "target": { "kind": "testId", "value": "account" } }
              ]
            }
            """);

        Assert.NotNull(plan);
        Assert.Equal("password", plan.Actions[0].SecretName);
        Assert.Null(plan.Actions[0].Text);
    }
}
