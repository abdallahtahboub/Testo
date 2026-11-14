using System.Text;
using Testo.Contracts.Planning;

namespace Testo.Benchmark;

/// <summary>
/// Writes a plan back out in the JSON a model would have sent.
/// </summary>
/// <remarks>
/// So that a recording saved from a real answer is read by exactly the parser a real answer goes
/// through. Writing the recordings in some other shape would let the parser pass against JSON
/// nobody will ever send it.
/// </remarks>
public static class PlanRecording
{
    /// <summary>The plan as a model's answer.</summary>
    public static string Write(TestPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var text = new StringBuilder();

        text.Append("{\n  \"scenarioName\": ").Append(Quote(plan.ScenarioName)).Append(",\n");
        text.Append("  \"notes\": [")
            .Append(string.Join(", ", plan.Notes.Select(Quote)))
            .Append("],\n");
        text.Append("  \"actions\": [\n");

        for (var index = 0; index < plan.Actions.Count; index++)
        {
            text.Append("    ").Append(Action(plan.Actions[index]));
            text.Append(index == plan.Actions.Count - 1 ? "\n" : ",\n");
        }

        text.Append("  ]\n}\n");

        return text.ToString();
    }

    private static string Action(PlannedAction action)
    {
        var fields = new List<string>
        {
            $"\"kind\": {Quote(Kind(action.Kind))}",
            $"\"description\": {Quote(action.Description)}",
        };

        if (action.Target is { } target)
        {
            fields.Add(target.Kind == TargetKind.TestId
                ? $"\"target\": {{ \"kind\": \"testId\", \"value\": {Quote(target.Value)} }}"
                : $"\"target\": {{ \"kind\": \"roleAndName\", \"role\": {Quote(target.Role ?? "")}, \"value\": {Quote(target.Value)} }}");
        }

        if (action.Path is { } path)
        {
            fields.Add($"\"path\": {Quote(path)}");
        }

        if (action.Text is { } text)
        {
            fields.Add($"\"text\": {Quote(text)}");
        }

        if (action.SecretName is { } secret)
        {
            fields.Add($"\"secretName\": {Quote(secret)}");
        }

        return "{ " + string.Join(", ", fields) + " }";
    }

    /// <summary>The word a model writes for an action.</summary>
    private static string Kind(ActionKind kind) => kind switch
    {
        ActionKind.Goto => "goto",
        ActionKind.Click => "click",
        ActionKind.Type => "type",
        ActionKind.Press => "press",
        ActionKind.WaitFor => "waitFor",
        ActionKind.AssertText => "assertText",
        ActionKind.AssertVisible => "assertVisible",
        ActionKind.AssertUrl => "assertUrl",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "There is no word for that action."),
    };

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
