using Autestiq.Contracts.Execution;
using Autestiq.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Autestiq.Ai;
using Autestiq.Benchmark;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Autestiq.Contracts.Planning;
using Autestiq.DemoShop;

// Runs the AI benchmark and writes docs/ai-benchmark.md.
//
//   dotnet run --project tools/Autestiq.Benchmark                      the baseline only
//   PLANNER__APIKEY=sk-... dotnet run --project tools/Autestiq.Benchmark    baseline and planner
//   dotnet run --project tools/Autestiq.Benchmark -- --unseen          the set that decides the gate;
//                                                                   add --target booking or invoices
//   dotnet run --project tools/Autestiq.Benchmark -- --recorded        replays saved answers, free
//   dotnet run --project tools/Autestiq.Benchmark -- --smoke           one scenario, for CI
//   dotnet run --project tools/Autestiq.Benchmark -- --self-check      proves the harness catches a
//                                                                   planner that only pretends
//
// It starts the demo shop itself, on a port of its own, and switches that shop's defects as it
// goes. Nothing else needs to be running, and it never touches the database or the web
// application: what is being measured is planning and execution, not the product's plumbing.

var options = BenchmarkOptions.From(args);

// Three targets, each run in this process so that its state and its defects are set from here: the demo
// shop (the first application), the booking clinic (the second) and the invoice desk (the third, built
// after the explorer's rules were settled).
// --without-test-ids serves the target with its test ids removed, to see what the product makes of an
// application that has none, as most do.
var withoutTestIds = args.Contains("--without-test-ids", StringComparer.Ordinal);

Action<WebApplication> map = options.Target switch
{
    "booking" => app => Autestiq.BookingShop.BookingShopRoutes.Map(app),
    "invoices" => app => Autestiq.InvoiceDesk.InvoiceDeskRoutes.Map(app),
    _ => app => DemoShopRoutes.Map(app),
};

// eShopOnWeb (Microsoft's sample, ADR-0024) is not ours: it runs on its own, and --origin says where. It has no
// test ids and no defect switches, so it measures planning on a sound application nobody here wrote.
var external = options.Target == "eshop"
    ? BenchmarkOptions.Value(args, "--origin") ?? "http://127.0.0.1:5106"
    : null;

using var host = external is null ? InProcessHost.Start(map, withoutTestIds) : null;
var origin = external ?? host!.Origin; // host is started exactly when there is no external origin.

Console.WriteLine(options.Target switch
{
    "booking" => $"The booking clinic is at {origin}.",
    "invoices" => $"The invoice desk is at {origin}.",
    "eshop" => $"eShopOnWeb is expected at {origin}; it is started separately.",
    _ => $"The demo shop is at {origin}.",
});

if (options.SelfCheck)
{
    return await SelfCheck.RunAsync(origin) ? 0 : 1;
}

// The clinic and the desk keep their state on the server, so anything that explores or runs against
// them puts them back first.
Func<Task>? startAfresh = options.Target switch
{
    "booking" => () =>
    {
        Autestiq.BookingShop.Clinic.Reset();
        return Task.CompletedTask;
    },
    "invoices" => () =>
    {
        Autestiq.InvoiceDesk.Ledger.Reset();
        return Task.CompletedTask;
    },
    _ => null,
};

Func<IReadOnlyList<string>, bool> setDefects = options.Target switch
{
    "booking" => names => Autestiq.BookingShop.Defects.Set(names),
    "invoices" => names => Autestiq.InvoiceDesk.Defects.Set(names),
    "eshop" => names => names.Count == 0,
    _ => names => Autestiq.DemoShop.Defects.Set(names),
};

// --unseen picks the set that decides the ADR-0020 gate, for whichever target is named (UnseenScenarios).
var unseen = args.Contains("--unseen", StringComparer.Ordinal);

IReadOnlyList<BenchmarkScenario> targetScenarios = unseen && options.Target != "eshop" ? UnseenScenarios.For(options.Target) : options.Target switch
{
    "booking" => BookingScenarios.All,
    "invoices" => InvoiceScenarios.All,
    "eshop" => EshopScenarios.All,
    _ => options.HeldOut ? HeldOutScenarios.All : BenchmarkScenarios.All,
};

// How much of what the baseline plans need the exploration shows: no key, nothing spent.
if (args.Contains("--coverage", StringComparer.Ordinal))
{
    var (snapshot, said) = await Coverage.ExploreAsync(origin, startAfresh);
    Console.WriteLine(said);

    if (withoutTestIds)
    {
        // The baselines name elements by test id, which this copy has lost. A second copy, with them,
        // says what each test id is: its role, its name, its text. The question is whether the copy without
        // them still shows that element.
        using var reference = InProcessHost.Start(map, withoutTestIds: false);
        var (withIds, _) = await Coverage.ExploreAsync(reference.Origin, startAfresh);
        Coverage.PrintWithoutTestIds(targetScenarios, snapshot, withIds, Console.Out);
    }
    else
    {
        Coverage.Print(targetScenarios, snapshot, Console.Out);
    }

    return 0;
}

// ADR-0023 §4: every baseline exported to Python, run by pytest with each defect off and on, must give the plan's
// verdict. --python names the interpreter that has pytest-playwright installed.
if (BenchmarkOptions.Value(args, "--export-check") is { } language)
{
    if (!string.Equals(language, "python", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"There is no exporter for '{language}'. Built so far: python.");
        return 2;
    }

    return await ExportCheck.RunAsync(
        origin,
        targetScenarios,
        setDefects,
        startAfresh,
        BenchmarkOptions.Value(args, "--python") ?? "python3",
        Console.Out) ? 0 : 1;
}

// What the exploration sees of the target, printed, with no planner and nothing spent. The way to check
// the crawl against an application without a key.
if (args.Contains("--explore", StringComparer.Ordinal))
{
    var (snapshot, said) = await Coverage.ExploreAsync(origin, startAfresh);
    Console.WriteLine(said);

    foreach (var page in snapshot.Pages)
    {
        var route = page.ReachedBy is { Count: > 0 } steps ? string.Join(" > ", steps) : "(by link)";
        Console.WriteLine($"  {page.Path,-48} {route}  [{page.Elements.Count} elements]");
    }

    // What a plan could name (ADR-0019, ADR-0024): by test id; else by role and name, or by exact text,
    // when that is unique on its page or the element carries the text of the container it is in.
    var all = snapshot.Pages.SelectMany(page => page.Elements.Select(element => (page, element))).ToList();

    // Settled by the crawl against the page, element by element (NamedBy).
    var byTestId = all.Count(item => item.element.NamedBy == TargetKind.TestId);
    var byRole = all.Count(item => item.element.NamedBy == TargetKind.RoleAndName);
    var byText = all.Count(item => item.element.NamedBy == TargetKind.Text);

    Console.WriteLine($"Elements described: {all.Count}. A plan could name {byTestId} by test id, {byRole} by role and name, "
        + $"{byText} by text; {all.Count - byTestId - byRole - byText} not at all.");

    // What nothing reaches reliably, so that it can be looked at: where a test id would help most.
    foreach (var (page, element) in all.Where(item => item.element.NamedBy is null).Take(25))
    {
        Console.WriteLine($"    not reachable: {page.Path} {element.Role} \"{element.Name}\"" + (element.Within is { } w ? $" within \"{w}\"" : ""));
    }

    return 0;
}

var planner = Planner(options);
// The held-out set is measured once, against the frozen scenarios and baselines (HeldOutScenarios).
IReadOnlyList<BenchmarkScenario> scenarios = targetScenarios;

if (options.Smoke && options.Target == "demo" && !options.HeldOut)
{
    scenarios = [BenchmarkScenarios.All[0]];
}

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
    SetDefects = setDefects,
    ResetTarget = startAfresh,
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

        // Why a run that did not pass did not pass. A failure that is only a missing element looks the
        // same as a failure to find the defect, and only the reason tells them apart.
        if (measurement.Attempts.Count > 0
            && measurement.Attempts[0].Status != RunStatus.Passed
            && measurement.Attempts[0].Reason is { Length: > 0 } reason)
        {
            Console.WriteLine($"      because: {reason.Split('\n')[0]}");
        }
    },
};

using var stopping = new CancellationTokenSource(TimeSpan.FromMinutes(options.Smoke ? 5 : 180));
var measurements = await harness.RunAsync(scenarios, stopping.Token);

Console.WriteLine($"\nFinished in {clock.Elapsed.TotalMinutes:0.0} minutes.");

var report = Report.Write(
    measurements,
    options.Repetitions,
    DateTimeOffset.UtcNow,
    external is null ? "an application of our own" : "eShopOnWeb, Microsoft's sample shop, which nobody here wrote");

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
    PlanningErrors.ProviderOutOfCredit => "the provider account is out of credit: the run is incomplete, not failed",
    PlanningErrors.ProviderRateLimited => "the provider asked for fewer requests (429): wait and run again",
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

    var model = Environment.GetEnvironmentVariable("PLANNER__MODEL") ?? "claude-sonnet-5-5";

    // The model's prices come from the price book's first version (ADR-0029), so that the benchmark's cost per plan and
    // the product's cost rows use the same figures.
    var prices = Autestiq.Domain.Costs.PriceCatalog.FirstPrices;
    var planner = new PlannerOptions
    {
        ApiKey = Environment.GetEnvironmentVariable("PLANNER__APIKEY"),
        Model = model,
        InputCostPerMillion = prices.FirstOrDefault(price => price.Item == Autestiq.Domain.Costs.CostItems.ModelInputToken(model))?.Price,
        OutputCostPerMillion = prices.FirstOrDefault(price => price.Item == Autestiq.Domain.Costs.CostItems.ModelOutputToken(model))?.Price,
    };

    if (string.IsNullOrWhiteSpace(planner.ApiKey))
    {
        return null;
    }

    if (!planner.IsConfigured)
    {
        throw new InvalidOperationException(
            $"The price book has no price for {model}, so its cost could not be counted. Use a model it prices, or publish its prices first.");
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
    bool FailOnFalsePositive,
    bool HeldOut,
    string Target)
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
            // The held-out run has its own report, so that it can never overwrite the first one.
            Value(args, "--report")
                ?? Path.Combine(root, "docs", ReportFileName(args)),

            // Where a run against a provider writes its answers, so that a later --recorded run
            // can replay them for nothing. Nothing is committed here yet and CI does not replay
            // them: it runs --self-check only.
            Value(args, "--recordings") ?? Path.Combine(root, "tools", "Autestiq.Benchmark", "recordings"),
            args.Contains("--fail-on-false-positive", StringComparer.Ordinal),
            args.Contains("--held-out", StringComparer.Ordinal),
            Value(args, "--target") ?? "demo");
    }

    /// <summary>Each target and each set has its own report, so no run can overwrite another's.</summary>
    private static string ReportFileName(string[] args) =>
        args.Contains("--unseen", StringComparer.Ordinal) ? $"ai-benchmark-unseen-{Value(args, "--target") ?? "demo"}.md"
        : Value(args, "--target") == "booking" ? "ai-benchmark-booking.md"
        : Value(args, "--target") == "invoices" ? "ai-benchmark-invoices.md"
        : Value(args, "--target") == "eshop" ? "ai-benchmark-eshop.md"
        : args.Contains("--held-out", StringComparer.Ordinal) ? "ai-benchmark-held-out.md"
        : "ai-benchmark.md";

    internal static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>The repository, found the same way the tests find it.</summary>
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Autestiq.slnx")))
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}

