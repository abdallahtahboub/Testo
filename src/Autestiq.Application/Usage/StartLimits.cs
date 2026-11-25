using Microsoft.EntityFrameworkCore;
using Autestiq.Domain.Execution;
using Autestiq.Persistence;

namespace Autestiq.Application.Usage;

/// <summary>
/// Rate limits on the paths that spend money or reach out (Milestone 3.6, gap G14): starting runs and asking for
/// proof checks. Counted in the database from what was asked for, under a lock on the organization's row, so they
/// hold across servers and under parallel requests, which an in-memory limiter does not.
/// </summary>
public static class StartLimits
{
    /// <summary>The window the limits count in.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Takes the organization's row lock for the rest of the transaction: one start at a time per organization, so
    /// the count below cannot be passed by two requests at once.
    /// </summary>
    public static Task LockAsync(AutestiqDbContext database, Guid organizationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // The organization's own row by its id; nothing here reads another tenant's data.
        return database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM organizations WHERE id = {organizationId} FOR UPDATE",
            cancellationToken);
    }

    /// <summary>Whether starting another run now would pass the organization's or the person's limit.</summary>
    public static async Task<bool> RunStartsExceededAsync(
        AutestiqDbContext database,
        Guid organizationId,
        Guid? userId,
        UsageOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var since = now - Window;

        // Held to the organization in hand; the tenant filter would do the same for a person, not for the dispatcher.
        var recent = database.TestRuns.IgnoreQueryFilters()
            .Where(run => run.OrganizationId == organizationId && run.QueuedAt >= since);

        if (await recent.CountAsync(cancellationToken).ConfigureAwait(false) >= options.StartsPerOrganizationPerMinute)
        {
            return true;
        }

        return userId is { } person
            && await recent.CountAsync(run => run.RequestedBy == person, cancellationToken).ConfigureAwait(false) >= options.StartsPerUserPerMinute;
    }

    /// <summary>Whether issuing or checking another proof now would pass the organization's limit.</summary>
    public static async Task<bool> ProofRequestsExceededAsync(
        AutestiqDbContext database,
        Guid organizationId,
        UsageOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var since = now - Window;

        var recent = await database.OriginProofs.IgnoreQueryFilters()
            .CountAsync(proof => proof.OrganizationId == organizationId && (proof.IssuedAt >= since || proof.CheckRequestedAt >= since), cancellationToken)
            .ConfigureAwait(false);

        return recent >= options.StartsPerOrganizationPerMinute;
    }
}
