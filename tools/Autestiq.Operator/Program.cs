using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application;
using Autestiq.Application.Costs;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Costs;
using Autestiq.Persistence;

// Commands for operators, against the product's database (ADR-0028, ADR-0029).
//
//   set-plan <organization-slug> <plan-code> [version] --by <name>
//   set-prices [--browser-hour <usd>] [--storage-gb-month <usd>] [--model <id> <input-usd> <output-usd>]...
//              [--estimate] [--from <yyyy-MM-ddTHH:mm:ssZ>] --note <text> --by <name>
//   cost-report [yyyy-MM] [--eur-rate <euros per dollar>]
//   migrations                       which migrations the database has, and which it lacks
//   migrate                          applies every migration it lacks (Milestone 5.1 R2)
//   migrate --to <migration>|0 --yes moves to an earlier migration, or to an empty schema: a rollback
//
// The web host never migrates a deployed database itself: run migrate before starting a new version.
//
// Run them with: dotnet run --project tools/Autestiq.Operator -- <command> ...
// The database is the one ConnectionStrings__Autestiq names. --by is who made the change, for the record.

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Autestiq");

if (connection is null)
{
    Console.Error.WriteLine("Set ConnectionStrings__Autestiq to the product's database.");
    return 2;
}

var services = new ServiceCollection()
    .AddLogging()
    .AddSingleton<ICurrentUser>(OperatorPrincipal.Instance)
    .AddSingleton<IOrganizationContext>(OperatorPrincipal.Instance)
    .AddAutestiqApplication(connection)
    .BuildServiceProvider();

await using (services.ConfigureAwait(false))
{
    using var scope = services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutestiqDbContext>();

    return args switch
    {
        ["set-plan", var slug, var plan, .. var rest] => await SetPlanAsync(database, scope.ServiceProvider.GetRequiredService<TenantWriteScope>(), slug, plan, rest).ConfigureAwait(false),
        ["set-prices", .. var rest] => await SetPricesAsync(database, rest).ConfigureAwait(false),
        ["cost-report", .. var rest] => await CostReportAsync(database, rest).ConfigureAwait(false),
        ["migrations"] => await MigrationsAsync(database).ConfigureAwait(false),
        ["migrate", .. var rest] => await MigrateAsync(database, rest).ConfigureAwait(false),
        _ => Usage(),
    };
}

static async Task<int> MigrationsAsync(AutestiqDbContext database)
{
    var (applied, pending) = await Autestiq.Application.Operations.DatabaseMigrations.StateAsync(database, CancellationToken.None).ConfigureAwait(false);

    foreach (var name in applied)
    {
        Console.WriteLine($"applied  {name}");
    }

    foreach (var name in pending)
    {
        Console.WriteLine($"pending  {name}");
    }

    return 0;
}

static async Task<int> MigrateAsync(AutestiqDbContext database, string[] rest)
{
    var target = Option(rest, "--to");
    var (applied, pending) = await Autestiq.Application.Operations.DatabaseMigrations.StateAsync(database, CancellationToken.None).ConfigureAwait(false);

    // Stepping back drops what later migrations added, data with it: never without saying so.
    var goesBack = target is not null && (target == Autestiq.Application.Operations.DatabaseMigrations.Empty
        || applied.Contains(target, StringComparer.Ordinal) && applied[^1] != target);

    if (goesBack && !rest.Contains("--yes", StringComparer.Ordinal))
    {
        Console.Error.WriteLine($"Moving back to {target} removes what later migrations added, data included. Take a backup, then repeat with --yes.");
        return 2;
    }

    try
    {
        await Autestiq.Application.Operations.DatabaseMigrations.MigrateAsync(database, target, CancellationToken.None).ConfigureAwait(false);
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }

    var (now, left) = await Autestiq.Application.Operations.DatabaseMigrations.StateAsync(database, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Was at {(applied.Count > 0 ? applied[^1] : "an empty schema")} with {pending.Count} pending; now at {(now.Count > 0 ? now[^1] : "an empty schema")} with {left.Count} pending.");
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("""
        Usage:
          set-plan <organization-slug> <plan-code> [version] --by <name>
          set-prices [--browser-hour <usd>] [--storage-gb-month <usd>] [--model <id> <input-usd> <output-usd>]... [--estimate] [--from <instant>] --note <text> --by <name>
          cost-report [yyyy-MM] [--eur-rate <euros per dollar>]
          migrations
          migrate [--to <migration>|0 --yes]
        """);
    return 2;
}

static async Task<int> SetPlanAsync(AutestiqDbContext database, TenantWriteScope writeScope, string slug, string plan, string[] rest)
{
    var by = Option(rest, "--by");
    var versionText = rest.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal) && argument != by);

    if (by is null)
    {
        Console.Error.WriteLine("Say who is making the change with --by.");
        return 2;
    }

    int? version = null;

    if (versionText is not null)
    {
        if (!int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            Console.Error.WriteLine($"'{versionText}' is not a version number.");
            return 2;
        }

        version = number;
    }

    var outcome = await PlanAssignment.MoveAsync(database, writeScope, TimeProvider.System, slug, plan, version, by, CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine(outcome switch
    {
        PlanChange.Moved => $"Moved {slug} to {plan}{(version is { } v ? $" version {v}" : "")}. Recorded in its audit log.",
        PlanChange.Unchanged => $"{slug} is already on that version. Nothing changed.",
        PlanChange.NoSuchOrganization => $"No organization is called {slug}.",
        PlanChange.NoSuchPlan => $"No plan {plan}{(version is { } n ? $" with a version {n}" : "")}.",
        PlanChange.NotYetEffective => "That version has not taken effect yet.",
        _ => outcome.ToString(),
    });

    return outcome is PlanChange.Moved or PlanChange.Unchanged ? 0 : 1;
}

static async Task<int> SetPricesAsync(AutestiqDbContext database, string[] rest)
{
    var by = Option(rest, "--by");
    var note = Option(rest, "--note");

    if (by is null || note is null)
    {
        Console.Error.WriteLine("Say who is publishing the prices with --by, and where they came from with --note.");
        return 2;
    }

    // --estimate marks the machine and storage figures given here as estimates; model prices are list prices.
    var estimate = rest.Contains("--estimate", StringComparer.Ordinal);
    var changes = new List<PriceChange>();

    if (Option(rest, "--browser-hour") is { } hour)
    {
        if (Money(hour) is not { } price)
        {
            return NotMoney(hour);
        }

        changes.Add(new PriceChange(CostItems.BrowserSecond, price, PriceCatalog.SecondsPerHour, estimate));
    }

    if (Option(rest, "--storage-gb-month") is { } storage)
    {
        if (Money(storage) is not { } price)
        {
            return NotMoney(storage);
        }

        changes.Add(new PriceChange(CostItems.StorageByteMonth, price, PriceCatalog.BytesPerGigabyte, estimate));
    }

    for (var index = Array.IndexOf(rest, "--model"); index >= 0; index = Array.IndexOf(rest, "--model", index + 1))
    {
        if (index + 3 >= rest.Length || Money(rest[index + 2]) is not { } input || Money(rest[index + 3]) is not { } output)
        {
            Console.Error.WriteLine("--model takes a model id, then its input and output price per million tokens.");
            return 2;
        }

        changes.Add(new PriceChange(CostItems.ModelInputToken(rest[index + 1]), input, PriceCatalog.TokensPerMillion, Estimate: false));
        changes.Add(new PriceChange(CostItems.ModelOutputToken(rest[index + 1]), output, PriceCatalog.TokensPerMillion, Estimate: false));
    }

    DateTimeOffset? from = null;

    if (Option(rest, "--from") is { } fromText)
    {
        if (!DateTimeOffset.TryParse(fromText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            Console.Error.WriteLine($"'{fromText}' is not an instant.");
            return 2;
        }

        from = parsed;
    }

    if (changes.Count == 0)
    {
        Console.Error.WriteLine("Give at least one price to change.");
        return 2;
    }

    var (outcome, version) = await PriceBook.PublishAsync(database, TimeProvider.System, changes, from, note, by, CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine(outcome switch
    {
        PricePublication.Published when version is not null => $"Published price version {version.Number}, in force from {version.EffectiveFrom:u}. Costs already recorded keep their prices.",
        PricePublication.InThePast => "A price version cannot take effect in the past: costs already recorded used the prices in force then.",
        PricePublication.NotAPrice => "Every price must be zero or more.",
        PricePublication.Unchanged => "Those are the prices already in the latest version. Nothing was published.",
        _ => outcome.ToString(),
    });

    return outcome == PricePublication.Published ? 0 : 1;
}

static async Task<int> CostReportAsync(AutestiqDbContext database, string[] rest)
{
    var month = rest.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal) && argument != Option(rest, "--eur-rate"))
        ?? UsageMeter.PeriodOf(TimeProvider.System.GetUtcNow());

    if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
    {
        Console.Error.WriteLine($"'{month}' is not a month: write it as yyyy-MM.");
        return 2;
    }

    decimal? euros = null;

    if (Option(rest, "--eur-rate") is { } rateText)
    {
        if (Money(rateText) is not { } rate || rate == 0)
        {
            return NotMoney(rateText);
        }

        euros = rate;
    }

    var lines = await CostReport.ForMonthAsync(database, month, CancellationToken.None).ConfigureAwait(false);
    Console.Write(CostReportText.Write(month, lines, euros));
    return 0;
}

static decimal? Money(string text) =>
    decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;

static int NotMoney(string text)
{
    Console.Error.WriteLine($"'{text}' is not an amount: write it with a point, such as 0.10.");
    return 2;
}

static string? Option(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

/// <summary>Nobody signed in and no organization in context: an operator works across organizations.</summary>
internal sealed class OperatorPrincipal : ICurrentUser, IOrganizationContext
{
    public static readonly OperatorPrincipal Instance = new();

    public bool IsSignedIn => false;

    public Guid? UserId => null;

    public string? Email => null;

    public string? DisplayName => null;

    public Role? Role => null;

    public Guid? OrganizationId => null;

    public bool Can(Permission permission) => false;

    public Guid RequireOrganizationId() => throw new InvalidOperationException("An operator command has no organization in context.");
}
