using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application;
using Autestiq.Application.Usage;
using Autestiq.Contracts.Identity;
using Autestiq.Persistence;

// Commands for operators, against the product's database (ADR-0028).
//
//   dotnet run --project tools/Autestiq.Operator -- set-plan <organization-slug> <plan-code> [version] --by <name>
//
// The database is the one ConnectionStrings__Autestiq names. --by is who made the change, for the audit log.

if (args is not ["set-plan", var slug, var plan, .. var rest])
{
    Console.Error.WriteLine("Usage: set-plan <organization-slug> <plan-code> [version] --by <name>");
    return 2;
}

var by = Option(rest, "--by");
var versionText = rest.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal) && argument != by);
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Autestiq");

if (by is null || connection is null)
{
    Console.Error.WriteLine("Say who is making the change with --by, and set ConnectionStrings__Autestiq.");
    return 2;
}

int? version = null;

if (versionText is not null)
{
    if (!int.TryParse(versionText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number))
    {
        Console.Error.WriteLine($"'{versionText}' is not a version number.");
        return 2;
    }

    version = number;
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

    var outcome = await PlanAssignment.MoveAsync(
            scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(),
            scope.ServiceProvider.GetRequiredService<TenantWriteScope>(),
            TimeProvider.System,
            slug,
            plan,
            version,
            by,
            CancellationToken.None)
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
