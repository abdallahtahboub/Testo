using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Testo.Contracts.Planning;

namespace Testo.Ai;

/// <summary>How to reach a model provider, and what its answers cost.</summary>
public sealed class PlannerOptions
{
    /// <summary>The configuration section this is bound from.</summary>
    public const string SectionName = "Planner";

    /// <summary>
    /// The API key. From configuration — an environment variable, a user secret, a secret store —
    /// and never from the repository, never in a <c>WorkerJob</c>, never in a log.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Which model to ask.</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>Where to ask it.</summary>
    public Uri Endpoint { get; set; } = new("https://api.anthropic.com/v1/messages");

    /// <summary>The provider's wire version.</summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>What a million input tokens costs, in US dollars, from the provider's price list.</summary>
    public decimal InputCostPerMillion { get; set; } = 3.00m;

    /// <summary>What a million output tokens costs, in US dollars.</summary>
    public decimal OutputCostPerMillion { get; set; } = 15.00m;

    /// <summary>How long to wait for an answer.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>Whether a key is configured at all, so a caller can say "no planner" rather than fail.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Asks Anthropic's API for a plan.
/// </summary>
/// <remarks>
/// <para>
/// One POST and <c>System.Text.Json</c>, rather than a provider SDK: the interface it implements is
/// where provider-independence lives, and an SDK would add a dependency, a release cadence and a
/// surface area for a request with four fields.
/// </para>
/// <para>
/// It knows nothing about runs, organizations or credentials for the application under test. It is
/// given a <see cref="PlanRequest"/> and returns what the model said, with what it cost. Judging
/// the answer is <c>PlanValidator</c>'s job, deliberately not this class's: an adapter that also
/// decided what was allowed would be a second place for the policy to live.
/// </para>
/// </remarks>
public sealed class AnthropicPlanner(HttpClient client, PlannerOptions options, TimeProvider timeProvider)
    : IScenarioPlanner
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string Model => options.Model;

    /// <inheritdoc />
    public async Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!options.IsConfigured)
        {
            return PlanResult.Failed([PlanningErrors.NoPlanner], PlanningCost.None, Model);
        }

        var started = timeProvider.GetTimestamp();

        try
        {
            using var message = Request(request);
            using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The status, never the body: a provider's error body can carry the prompt back,
                // and the prompt contains the application under test.
                //
                // 401 and 403 are told apart from everything else because they need the opposite
                // response. "Could not be reached" sends somebody to check their network when the
                // answer is a key in their console — which is exactly what happened the first time
                // a person ran this.
                var refused = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

                return PlanResult.Failed(
                    [refused ? PlanningErrors.ProviderRefusedTheKey : PlanningErrors.ProviderUnavailable],
                    Spent(0, 0, started),
                    Model);
            }

            var answer = await response.Content.ReadFromJsonAsync<Answer>(Wire, cancellationToken).ConfigureAwait(false);

            var input = answer?.Usage?.InputTokens ?? 0;
            var output = answer?.Usage?.OutputTokens ?? 0;
            var cost = Spent(input, output, started);

            var text = string.Concat(answer?.Content?.Where(block => block.Type == "text").Select(block => block.Text) ?? []);

            return PlanJson.Read(text) is { } plan
                ? PlanResult.From(plan, cost, Model)
                : PlanResult.Failed([PlanningErrors.Unreadable], cost, Model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Unreachable, or too slow. Not a fault in the plan, and not something a second
            // attempt would tell us anything new about.
            return PlanResult.Failed([PlanningErrors.ProviderUnavailable], Spent(0, 0, started), Model);
        }
    }

    private HttpRequestMessage Request(PlanRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(
                new Ask(
                    options.Model,
                    Math.Max(1_024, request.MaxTokens / 4),
                    PlanPrompt.System,
                    [new Turn("user", PlanPrompt.User(request))]),
                options: Wire),
        };

        message.Headers.Add("x-api-key", options.ApiKey);
        message.Headers.Add("anthropic-version", options.ApiVersion);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return message;
    }

    private PlanningCost Spent(int input, int output, long started) => new(
        input,
        output,
        Attempts: 1,
        Cost: (input * options.InputCostPerMillion / 1_000_000m) + (output * options.OutputCostPerMillion / 1_000_000m),
        Elapsed: timeProvider.GetElapsedTime(started));

    private sealed record Ask(
        string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        string System,
        IReadOnlyList<Turn> Messages);

    private sealed record Turn(string Role, string Content);

    private sealed record Answer(List<Block>? Content, Usage? Usage);

    private sealed record Block(string? Type, string? Text);

    private sealed record Usage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}

/// <summary>
/// A planner that replays an answer already on disk.
/// </summary>
/// <remarks>
/// <para>
/// The point of it is to let the whole path — prompt, parsing, validation, execution — be exercised
/// for nothing and with the same answer every time. A test whose input comes from a live model is
/// not a test; it is a sample.
/// </para>
/// <para>
/// <strong>No recordings are committed yet, and nothing replays them.</strong> The benchmark writes
/// one per scenario when it runs against a provider, but the only run so far happened on a
/// developer machine and its answers were not kept. CI runs <c>--self-check</c> and not
/// <c>--recorded</c>. So this class works and is tested, and the claim that the parser is held to
/// JSON a model actually produced is not true yet — it becomes true when a run's recordings are
/// committed.
/// </para>
/// </remarks>
public sealed class RecordedPlanner(IReadOnlyDictionary<string, string> answers, string model = "recorded")
    : IScenarioPlanner
{
    /// <inheritdoc />
    public string Model => model;

    /// <summary>Loads every <c>*.json</c> in a directory, keyed by file name without the extension.</summary>
    public static RecordedPlanner FromDirectory(string directory, string model = "recorded")
    {
        var answers = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.json")
                .ToDictionary(file => Path.GetFileNameWithoutExtension(file), File.ReadAllText, StringComparer.OrdinalIgnoreCase)
            : [];

        return new RecordedPlanner(answers, model);
    }

    /// <inheritdoc />
    public Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = Key(request.Scenario);

        if (!answers.TryGetValue(key, out var answer))
        {
            return Task.FromResult(PlanResult.Failed([PlanningErrors.NoPlanner], PlanningCost.None, Model));
        }

        // Nothing was spent, and the table should say so rather than pretend a price.
        return Task.FromResult(
            PlanJson.Read(answer) is { } plan
                ? PlanResult.From(plan, PlanningCost.None with { Attempts = 1 }, Model)
                : PlanResult.Failed([PlanningErrors.Unreadable], PlanningCost.None with { Attempts = 1 }, Model));
    }

    /// <summary>
    /// A scenario's words as a file name: lower case, letters and digits, dashes between. So a
    /// recording is recognizable on disk and a scenario finds its own answer.
    /// </summary>
    public static string Key(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var key = new System.Text.StringBuilder(scenario.Length);
        var dash = false;

        foreach (var character in scenario.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                key.Append(character);
                dash = false;
            }
            else if (!dash && key.Length > 0)
            {
                key.Append('-');
                dash = true;
            }
        }

        return key.ToString().Trim('-') is { Length: > 0 } trimmed
            ? trimmed[..Math.Min(80, trimmed.Length)]
            : "scenario";
    }
}

/// <summary>
/// A planner that answers nothing, for a product that has no key configured.
/// </summary>
/// <remarks>
/// Registered when nothing else is, so that asking for a plan with no provider says "no planner"
/// rather than throwing somewhere in a background service. A run that cannot be planned is a run
/// that says so.
/// </remarks>
public sealed class NoPlanner : IScenarioPlanner
{
    /// <inheritdoc />
    public string Model => "none";

    /// <inheritdoc />
    public Task<PlanResult> PlanAsync(PlanRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(PlanResult.Failed([PlanningErrors.NoPlanner], PlanningCost.None, Model));
}
