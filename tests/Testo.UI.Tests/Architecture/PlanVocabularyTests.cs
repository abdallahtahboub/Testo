using System.Reflection;
using Testo.Contracts.Planning;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// ADR-0019 §2 claims that no model output is ever executed as code, and that this holds because of
/// the shape of the data rather than because of a check somewhere. A claim like that is worth a test
/// that fails when somebody adds the field that would break it.
/// </summary>
public class PlanVocabularyTests
{
    /// <summary>
    /// Words that, as the name of a field on an action, would mean a plan could carry something to
    /// evaluate, a selector language, or an address of its own.
    /// </summary>
    private static readonly string[] ForbiddenInNames =
    [
        "script", "javascript", "js", "eval", "evaluate", "expression", "code", "command",
        "selector", "css", "xpath", "query", "url", "uri", "href", "origin", "host", "endpoint",
    ];

    [Fact]
    public void PlannedAction_IsTheWholeOfWhatAnActionCanSay()
    {
        // Pinned so that the two tests below cannot quietly become vacuous: if the shape changes,
        // this fails first and names what was added.
        Assert.Equal(
            ["Description", "Kind", "Path", "SecretName", "Target", "Text"],
            Fields(typeof(PlannedAction)).Select(property => property.Name).Order(StringComparer.Ordinal));

        Assert.Equal(
            ["Kind", "Role", "Value"],
            Fields(typeof(ActionTarget)).Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PlannedAction_HasNoFieldThatCouldCarryCodeOrASelector()
    {
        var offending = Fields(typeof(PlannedAction))
            .Concat(Fields(typeof(ActionTarget)))
            .Where(property => ForbiddenInNames.Any(word =>
                property.Name.Equals(word, StringComparison.OrdinalIgnoreCase)
                || property.Name.EndsWith(word, StringComparison.OrdinalIgnoreCase)))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        Assert.Empty(offending);
    }

    [Fact]
    public void PlannedAction_CarriesNothingButStringsAndItsOwnEnums()
    {
        // A nested object, a dictionary or a collection would be somewhere to hide structure that
        // nobody validates. Every value in an action is a string, an enum of ours, or the target.
        var allowed = new[] { typeof(string), typeof(ActionKind), typeof(TargetKind), typeof(ActionTarget), typeof(bool) };

        var offending = Fields(typeof(PlannedAction))
            .Concat(Fields(typeof(ActionTarget)))
            .Where(property => !allowed.Contains(property.PropertyType))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name} is {property.PropertyType.Name}")
            .ToList();

        Assert.Empty(offending);
    }

    [Fact]
    public void ActionKind_IsTheVocabularyTheAdrNames()
    {
        // The list is short on purpose, and ADR-0019 says growing it is a decision. This test is
        // where that decision gets noticed.
        string[] expected =
        [
            "Goto", "Click", "Type", "Press", "WaitFor", "AssertText", "AssertVisible", "AssertUrl",
            "AssertNotVisible",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), Enum.GetNames<ActionKind>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ActionKind_EveryNameHasItsOwnNumber_BecausePlansAreStored()
    {
        var values = Enum.GetValues<ActionKind>().Select(kind => (int)kind).ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
        Assert.DoesNotContain(0, values);
    }

    [Fact]
    public void SnapshotElement_SaysOnlyWhatAPersonWouldRead()
    {
        // The snapshot is untrusted input that goes into a prompt on purpose (ADR-0019 §5), so its
        // fields are attack surface. Four of them, all plain text.
        Assert.Equal(
            ["Name", "Role", "TestId", "Text"],
            Fields(typeof(SnapshotElement)).Select(property => property.Name).Order(StringComparer.Ordinal));

        Assert.All(Fields(typeof(SnapshotElement)), property => Assert.Equal(typeof(string), property.PropertyType));
    }

    [Fact]
    public void PlanRequest_TellsThePlannerNothingAboutTheCustomer()
    {
        // A planner is given a scenario and a description of an application. Not an organization,
        // not a run, not a credential — only the *names* of the values a run holds.
        var names = Fields(typeof(PlanRequest)).Select(property => property.Name).ToList();

        Assert.DoesNotContain("OrganizationId", names);
        Assert.DoesNotContain("RunId", names);
        Assert.DoesNotContain("Secrets", names);
        Assert.Contains("SecretNames", names);
    }

    [Fact]
    public void WorkerJob_NeverCarriesAModelKey()
    {
        // ADR-0018 treats the worker as already compromised; ADR-0019 §4 therefore keeps our model
        // account out of its reach. A key in the job would hand an attacker who owns one run a bill
        // on every other one.
        var names = Fields(typeof(Contracts.Execution.WorkerJob)).Select(property => property.Name).ToList();

        Assert.DoesNotContain(names, name => name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Model", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<PropertyInfo> Fields(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract" && property.GetMethod?.IsSpecialName != true
                || property.CanRead)
            .Where(property => !property.Name.StartsWith("Is", StringComparison.Ordinal))
            .Where(property => property.Name is not ("AssertionCount" or "TotalTokens"));
}
