using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Testo.Ai;
using Testo.Benchmark;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testo.Contracts.Planning;
using Testo.DemoShop;

// Runs the AI benchmark and writes docs/ai-benchmark.md.
//
//   dotnet run --project tools/Testo.Benchmark                      the baseline only
//   PLANNER__APIKEY=sk-... dotnet run --project tools/Testo.Benchmark    baseline and planner
//   dotnet run --project tools/Testo.Benchmark -- --recorded        replays saved answers, free
//   dotnet run --project tools/Testo.Benchmark -- --smoke           one scenario, for CI
//   dotnet run --project tools/Testo.Benchmark -- --self-check      proves the harness catches a
//                                                                   planner that only pretends
//
// It starts the demo shop itself, on a port of its own, and switches that shop's defects as it
// goes. Nothing else needs to be running, and it never touches the database or the web
// application: what is being measured is planning and execution, not the product's plumbing.

var options = BenchmarkOptions.From(args);

using var shop = DemoShopHost.Start();
var origin = shop.Origin;

Console.WriteLine($"The demo shop is at {origin}.");

if (options.SelfCheck)
{
    return await SelfCheck.RunAsync(origin) ? 0 : 1;
}

var planner = Planner(options);
var scenarios = options.Smoke ? [BenchmarkScenarios.All[0]] : BenchmarkScenarios.All;

Console.WriteLine(planner is null
    ? "No planner: measuring the baseline only."
    : $"Planner: {planner.Model}.");

Console.WriteLine($"{scenarios.Count} scenario(s), {options.Repetitions} execution(s) each.");

if (planner is not null)
{
    // Said before the silence rather than after it: a planned run crawls the application and asks
    // a model for every scenario, and ten quiet minutes look exactly like a hang.
    Console.WriteLine(
        $"This takes a while: {scenarios.Count * 2} crawls, {scenarios.Count * 2} model calls and "
        + $"about {scenarios.Count * 4 * options.Repetitions} browser runs. Lines appear as each one finishes.");
}

Console.WriteLine();

var clock = System.Diagnostics.Stopwatch.StartNew();

var harness = new Harness(origin, planner, options.Repetitions, options.RecordingDirectory)
{
    Say = Console.WriteLine,
    Measured = measurement =>
    {
        // The reason, not just "no plan". Somebody watching lines scroll past should not have to
        // open a markdown file to find out that their key was refused.
        var outcome = measurement.PlanningErrors.Count > 0
            ? "no plan — " + string.Join(", ", measurement.PlanningErrors.Select(Explain))
            : measurement.Verdict.ToString();

        var cost = measurement.Planning.TotalTokens > 0
            ? $"  {measurement.Planning.TotalTokens} tokens, ${measurement.Planning.Cost:0.0000}"
            : "";

        Console.WriteLine(
            $"  {measurement.Scenario.Id,-34} defect={(measurement.DefectOn ? "on " : "off")} "
            + $"{measurement.Source,-18} {outcome}{cost}");
    },
};

using var stopping = new CancellationTokenSource(TimeSpan.FromMinutes(options.Smoke ? 5 : 60));
var measurements = await harness.RunAsync(scenarios, stopping.Token);

Console.WriteLine($"\nFinished in {clock.Elapsed.TotalMinutes:0.0} minutes.");

var report = Report.Write(measurements, options.Repetitions, DateTimeOffset.UtcNow);

if (options.Smoke)
{
    // CI's purpose here is to prove the harness works, not to publish numbers from one scenario.
    Console.WriteLine("\nSmoke run finished; the report is not written.");
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
    await File.WriteAllTextAsync(options.ReportPath, report, stopping.Token);
    Console.WriteLine($"\nWritten to {options.ReportPath}.");
}

// A false positive is the one outcome that must never pass unnoticed, so it is also the exit code.
var falsePositives = measurements.SelectMany(m => m.Attempts).Count(a => a.Verdict == Verdict.FalsePositive);

if (falsePositives > 0)
{
    Console.WriteLine($"\n{falsePositives} false positive(s): a run passed while the shop was broken.");
}

return falsePositives > 0 && options.FailOnFalsePositive ? 1 : 0;

// One sentence per resource key, for the console. The report has its own copy for markdown.
static string Explain(string error) => error switch
{
    PlanningErrors.ProviderRefusedTheKey =>
        "the provider refused the key (401 or 403): it is wrong, truncated, expired or revoked",
    PlanningErrors.ProviderUnavailable => "the provider could not be reached",
    PlanningErrors.NoPlanner => "no planner is configured",
    PlanningErrors.Unreadable => "the answer was not a plan",
    PlanningErrors.ForeignOrigin => "the plan named somewhere other than the shop",
    PlanningErrors.NoAssertions => "the plan checked nothing",
    PlanningErrors.TooManyActions => "the plan was longer than the budget",
    PlanningErrors.UnknownAction => "the plan used an action that does not exist",
    PlanningErrors.UnknownTarget => "a target was named in a way that is not allowed",
    PlanningErrors.Incomplete => "an action was missing something it needs",
    PlanningErrors.BudgetSpent => "planning spent the run's token or money budget",
    _ => error,
};

static IScenarioPlanner? Planner(BenchmarkOptions options)
{
    if (options.Recorded)
    {
        return RecordedPlanner.FromDirectory(options.RecordingDirectory ?? "", "recorded");
    }

    var planner = new PlannerOptions
    {
        ApiKey = Environment.GetEnvironmentVariable("PLANNER__APIKEY"),
        Model = Environment.GetEnvironmentVariable("PLANNER__MODEL") ?? "claude-sonnet-5-5",
    };

    if (!planner.IsConfigured)
    {
        return null;
    }

    var client = new HttpClient { Timeout = planner.Timeout };
    return new AnthropicPlanner(client, planner, TimeProvider.System);
}

/// <summary>What the benchmark was asked to do.</summary>
internal sealed record BenchmarkOptions(
    bool Smoke,
    bool Recorded,
    bool SelfCheck,
    int Repetitions,
    string ReportPath,
    string? RecordingDirectory,
    bool FailOnFalsePositive)
{
    /// <summary>Reads the arguments. Plain switches; this is a tool, not a product.</summary>
    public static BenchmarkOptions From(string[] args)
    {
        var smoke = args.Contains("--smoke", StringComparer.Ordinal);
        var recorded = args.Contains("--recorded", StringComparer.Ordinal);

        var repetitions = Value(args, "--repetitions") is { } text && int.TryParse(text, out var parsed)
            ? Math.Clamp(parsed, 1, 10)
            : smoke ? 1 : 2;

        var root = Root();

        return new BenchmarkOptions(
            smoke,
            recorded,
            args.Contains("--self-check", StringComparer.Ordinal),
            repetitions,
            Value(args, "--report") ?? Path.Combine(root, "docs", "ai-benchmark.md"),

            // Recordings live beside the tool, in the repository: they are real answers, and CI
            // replays them so that the whole path is exercised for nothing.
            Value(args, "--recordings") ?? Path.Combine(root, "tools", "Testo.Benchmark", "recordings"),
            args.Contains("--fail-on-false-positive", StringComparer.Ordinal));
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>The repository, found the same way the tests find it.</summary>
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Testo.slnx")))
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}

/// <summary>The demo shop, started in this process on a port of its own.</summary>
internal sealed class DemoShopHost : IDisposable
{
    private readonly WebApplication _app;

    private DemoShopHost(WebApplication app, string origin)
    {
        _app = app;
        Origin = origin;
    }

    /// <summary>Where it is listening.</summary>
    public string Origin { get; }

    /// <summary>
    /// Starts it. The shop's own <c>Program</c> is an executable, so the benchmark builds the same
    /// routes through its public helpers rather than shelling out to it.
    /// </summary>
    public static DemoShopHost Start()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        DemoShopRoutes.Map(app);
        app.StartAsync().GetAwaiter().GetResult();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The shop does not report its address.");

        return new DemoShopHost(app, addresses.Addresses.First().TrimEnd('/'));
    }

    public void Dispose()
    {
        _app.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_app).Dispose();
    }
}
