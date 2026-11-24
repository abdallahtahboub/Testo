using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Autestiq.Persistence;

namespace Autestiq.Application.Operations;

/// <summary>
/// Applying the schema outside development (Milestone 5.1 R2). The web host never migrates a deployed database at
/// start: an operator runs <c>migrate</c> before the new version starts, and can step back with <c>migrate --to</c>.
/// </summary>
public static class DatabaseMigrations
{
    /// <summary>The target that removes every migration: an empty schema.</summary>
    public const string Empty = "0";

    /// <summary>The migrations the database has, and the ones it still lacks, in order.</summary>
    public static async Task<(IReadOnlyList<string> Applied, IReadOnlyList<string> Pending)> StateAsync(AutestiqDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var applied = (await database.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        return (applied, pending);
    }

    /// <summary>Every migration this build knows, in order.</summary>
    public static IReadOnlyList<string> Known(AutestiqDbContext database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return [.. database.Database.GetMigrations()];
    }

    /// <summary>
    /// Moves the schema to <paramref name="target"/>: the latest migration when <c>null</c>, an earlier one to step
    /// back, or <see cref="Empty"/>. Each migration runs in its own transaction, as Entity Framework applies them.
    /// </summary>
    /// <exception cref="ArgumentException">The target is not a migration this build knows.</exception>
    public static async Task MigrateAsync(AutestiqDbContext database, string? target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (target is not null && target != Empty && !Known(database).Contains(target, StringComparer.Ordinal))
        {
            throw new ArgumentException($"There is no migration named {target}.", nameof(target));
        }

        await database.GetService<IMigrator>().MigrateAsync(target, cancellationToken).ConfigureAwait(false);
    }
}
