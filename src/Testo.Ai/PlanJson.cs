using System.Text.Json;
using System.Text.Json.Serialization;
using Testo.Contracts.Planning;

namespace Testo.Ai;

/// <summary>
/// Reads a model's answer into a plan, or fails. The one place a string from a provider becomes
/// something typed.
/// </summary>
/// <remarks>
/// It is strict on purpose. An unknown action kind, a target shape nobody defined, a number where
/// a string belongs: all of them fail here rather than becoming a default that quietly means
/// something. The validator then judges what did parse.
/// </remarks>
public static class PlanJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Turns the answer into a plan, or returns <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Models like to wrap JSON in prose or a fenced block even when asked not to, so the object
    /// is found rather than assumed to be the whole answer. That is leniency about packaging, not
    /// about content: what is inside is read strictly.
    /// </remarks>
    public static TestPlan? Read(string answer)
    {
        if (Extract(answer) is not { Length: > 0 } json)
        {
            return null;
        }

        try
        {
            var wire = JsonSerializer.Deserialize<WirePlan>(json, Options);

            if (wire?.Actions is null or { Count: 0 })
            {
                return null;
            }

            var actions = new List<PlannedAction>(wire.Actions.Count);

            foreach (var action in wire.Actions)
            {
                if (Convert(action) is not { } planned)
                {
                    // One unreadable action makes the plan unreadable. Dropping it would change
                    // what the plan does and nobody asked us to edit it.
                    return null;
                }

                actions.Add(planned);
            }

            return new TestPlan(
                string.IsNullOrWhiteSpace(wire.ScenarioName) ? "A plan" : wire.ScenarioName.Trim(),
                actions,
                wire.Notes?.Where(note => !string.IsNullOrWhiteSpace(note)).ToList() ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The outermost JSON object in the answer, by brace depth.</summary>
    private static string? Extract(string answer)
    {
        var start = answer.IndexOf('{', StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = start; index < answer.Length; index++)
        {
            var character = answer[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            switch (character)
            {
                case '\\' when inString:
                    escaped = true;
                    break;

                case '"':
                    inString = !inString;
                    break;

                case '{' when !inString:
                    depth++;
                    break;

                case '}' when !inString:
                    depth--;

                    if (depth == 0)
                    {
                        return answer[start..(index + 1)];
                    }

                    break;

                default:
                    break;
            }
        }

        return null;
    }

    private static PlannedAction? Convert(WireAction action)
    {
        if (Kind(action.Kind) is not { } kind || string.IsNullOrWhiteSpace(action.Description))
        {
            return null;
        }

        ActionTarget? target = null;

        if (action.Target is { } wireTarget)
        {
            if (Target(wireTarget) is not { } converted)
            {
                return null;
            }

            target = converted;
        }

        return new PlannedAction(
            kind,
            action.Description.Trim(),
            target,
            Blank(action.Path),
            Blank(action.Text),
            Blank(action.SecretName));
    }

    /// <summary>The nine words a plan may use for an action. Anything else is not a plan.</summary>
    private static ActionKind? Kind(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        "goto" => ActionKind.Goto,
        "click" => ActionKind.Click,
        "type" => ActionKind.Type,
        "press" => ActionKind.Press,
        "waitfor" => ActionKind.WaitFor,
        "asserttext" => ActionKind.AssertText,
        "assertvisible" => ActionKind.AssertVisible,
        "asserturl" => ActionKind.AssertUrl,
        _ => null,
    };

    private static ActionTarget? Target(WireTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Value))
        {
            return null;
        }

        return target.Kind?.Trim().ToLowerInvariant() switch
        {
            "testid" => ActionTarget.ByTestId(target.Value.Trim()),

            "roleandname" when !string.IsNullOrWhiteSpace(target.Role) =>
                ActionTarget.ByRole(target.Role.Trim().ToLowerInvariant(), target.Value.Trim()),

            _ => null,
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record WirePlan(
        string? ScenarioName,
        List<string>? Notes,
        List<WireAction>? Actions);

    private sealed record WireAction(
        string? Kind,
        string? Description,
        WireTarget? Target,
        string? Path,
        string? Text,
        [property: JsonPropertyName("secretName")] string? SecretName);

    private sealed record WireTarget(string? Kind, string? Value, string? Role);
}
