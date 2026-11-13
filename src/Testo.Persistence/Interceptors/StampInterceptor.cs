using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testo.Contracts.Identity;
using Testo.Domain.Common;

namespace Testo.Persistence.Interceptors;

/// <summary>
/// Fills in who wrote a row and when (<c>docs/domain-model.md</c> §2), so that no use case has to
/// remember to.
/// </summary>
/// <remarks>
/// The time comes from <see cref="TimeProvider"/> rather than <c>DateTime.UtcNow</c>, so a test
/// can decide what "now" is and an expiry can be checked without waiting for it.
/// </remarks>
public sealed class StampInterceptor(TimeProvider timeProvider, ICurrentUser currentUser) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var actor = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = actor;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;

                case EntityState.Modified:
                    // Created* is set once. Saying so here means a use case cannot overwrite it
                    // by loading a row, changing it and saving the whole object back.
                    entry.Property(stamp => stamp.CreatedAt).IsModified = false;
                    entry.Property(stamp => stamp.CreatedBy).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;

                default:
                    break;
            }
        }
    }
}
