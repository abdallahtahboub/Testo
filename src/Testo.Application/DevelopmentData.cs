using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testo.Contracts.Identity;
using Testo.Domain.Identity;
using Testo.Domain.Projects;
using Testo.Persistence;
using Testo.Persistence.Identity;

namespace Testo.Application;

/// <summary>
/// Puts an organization and its people into an empty development database, so that signing in
/// works the first time somebody runs the application.
/// </summary>
/// <remarks>
/// <para>
/// The people are the ones in <c>docs/ux/users.md</c>, so a screen full of names reads like a
/// real QA team rather than like "User 1". Everyone shares one password, which is printed to the
/// log, because a developer who cannot sign in reaches for a shortcut that is worse.
/// </para>
/// <para>
/// This runs only when the database has no organizations, and only where the host asks for it —
/// never in production.
/// </para>
/// </remarks>
public static class DevelopmentData
{
    /// <summary>The password every seeded person has.</summary>
    public const string Password = "testo-development-password";

    private static readonly (string Email, string Name, Role Role)[] People =
    [
        ("mara@northwind.example", "Mara Lindqvist", Role.Owner),
        ("sven@northwind.example", "Sven Albrecht", Role.Admin),
        ("tom@northwind.example", "Tom Becker", Role.QaEngineer),
        ("nadia@northwind.example", "Nadia Haddad", Role.QaEngineer),
        ("jonas@northwind.example", "Jonas Wernicke", Role.Developer),
        ("claudia@northwind.example", "Claudia Mertens", Role.Viewer),
    ];

    /// <summary>Applies the migrations and seeds, if there is nothing there yet.</summary>
    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();

        var database = scope.ServiceProvider.GetRequiredService<TestoDbContext>();
        var credentials = scope.ServiceProvider.GetRequiredService<CredentialService>();
        var writeScope = scope.ServiceProvider.GetRequiredService<TenantWriteScope>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevelopmentData));

        await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        if (await database.Organizations.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var organization = new Organization
        {
            Name = "Northwind Software",
            Slug = "northwind",
            DataRegion = "eu-central",
            Plan = "trial",
        };

        database.Organizations.Add(organization);

        database.Add(new Project
        {
            OrganizationId = organization.Id,
            Name = "Northwind Shop",
            KeyPrefix = "SHOP",
            Description = "The web shop under test.",
        });

        foreach (var (email, name, role) in People)
        {
            var person = new User
            {
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                DisplayName = name,
                EmailConfirmed = true,
                Locale = "en",
            };

            var problems = await credentials.SetPasswordAsync(person, Password).ConfigureAwait(false);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The development password is not acceptable: {string.Join(", ", problems)}.");
            }

            database.Users.Add(person);
            database.Memberships.Add(new Membership
            {
                OrganizationId = organization.Id,
                UserId = person.Id,
                Role = role,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }

        // There is no request and therefore no current organization, so the writes into the one
        // being created are declared (D-2).
        using (writeScope.AllowWritingTo(organization.Id))
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        SeedLog.Seeded(logger, organization.Name, People[0].Email, Password);
    }
}

/// <summary>The one message the seeder writes.</summary>
internal static partial class SeedLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Seeded the development database with {Organization}. Sign in as {Email} with the password {Password}.")]
    public static partial void Seeded(ILogger logger, string organization, string email, string password);
}
