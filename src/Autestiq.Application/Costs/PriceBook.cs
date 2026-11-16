using Microsoft.EntityFrameworkCore;
using Autestiq.Domain.Costs;
using Autestiq.Persistence;

namespace Autestiq.Application.Costs;

/// <summary>What publishing a price version came to.</summary>
public enum PricePublication
{
    /// <summary>Published as the next version.</summary>
    Published = 1,

    /// <summary>It would have taken effect in the past, which would make costs already recorded disagree with it.</summary>
    InThePast = 2,

    /// <summary>A price was not positive, or was for no units.</summary>
    NotAPrice = 3,

    /// <summary>It changed nothing the version before it said.</summary>
    Unchanged = 4,
}

/// <summary>A price to set in a new version: <paramref name="Price"/> for every <paramref name="Per"/> units of an item.</summary>
public sealed record PriceChange(string Item, decimal Price, long Per, bool Estimate);

/// <summary>
/// Publishes price versions (Milestone 3.8, gap G11, ADR-0029). For operators only. A new version starts as a copy of
/// the latest one with the changes applied, so a change to one price leaves the others as they were; and it can only
/// take effect now or later, never earlier, so no cost already recorded is contradicted by the book.
/// </summary>
public static class PriceBook
{
    /// <summary>Publishes the next version, effective at <paramref name="effectiveFrom"/> or now.</summary>
    public static async Task<(PricePublication Outcome, PriceVersion? Version)> PublishAsync(
        AutestiqDbContext database,
        TimeProvider clock,
        IReadOnlyList<PriceChange> changes,
        DateTimeOffset? effectiveFrom,
        string note,
        string publishedBy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(changes);

        var now = clock.GetUtcNow();
        var from = effectiveFrom ?? now;

        if (from < now)
        {
            return (PricePublication.InThePast, null);
        }

        if (changes.Any(change => change.Price < 0 || change.Per <= 0 || string.IsNullOrWhiteSpace(change.Item)))
        {
            return (PricePublication.NotAPrice, null);
        }

        var latest = await database.PriceVersions
            .Include(version => version.Prices)
            .OrderByDescending(version => version.Number)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var prices = (latest?.Prices ?? [])
            .ToDictionary(price => price.Item, price => new PriceChange(price.Item, price.Price, price.Per, price.Estimate), StringComparer.Ordinal);

        var changed = false;

        foreach (var change in changes)
        {
            changed |= !prices.TryGetValue(change.Item, out var before) || before != change;
            prices[change.Item] = change;
        }

        if (!changed)
        {
            return (PricePublication.Unchanged, null);
        }

        var version = new PriceVersion
        {
            Number = (latest?.Number ?? 0) + 1,
            EffectiveFrom = from,
            Currency = latest?.Currency ?? PriceCatalog.Currency,
            PublishedBy = publishedBy,
            Note = note,
        };

        foreach (var price in prices.Values.OrderBy(price => price.Item, StringComparer.Ordinal))
        {
            version.Prices.Add(new UnitPrice
            {
                PriceVersionId = version.Id,
                Item = price.Item,
                Price = price.Price,
                Per = price.Per,
                Estimate = price.Estimate,
            });
        }

        database.PriceVersions.Add(version);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (PricePublication.Published, version);
    }
}
