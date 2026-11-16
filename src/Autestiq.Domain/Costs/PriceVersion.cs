using Autestiq.Domain.Common;

namespace Autestiq.Domain.Costs;

/// <summary>
/// What the things a run uses cost us, from a date (Milestone 3.8, gap G11, ADR-0029). Written once and never changed:
/// a change of price is a new version, so a cost recorded under an older one keeps the price it was recorded at.
/// Shared by every organization, outside the tenant boundary.
/// </summary>
public sealed class PriceVersion : Entity, IAppendOnly
{
    /// <summary>1 for the first version, then 2, 3 and on.</summary>
    public int Number { get; set; }

    /// <summary>From when it is the price in force. A cost uses the latest version in force when its run finished.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The currency of every price in it. US dollars, the currency the providers charge in.</summary>
    public required string Currency { get; set; }

    /// <summary>Who published it, for the record.</summary>
    public required string PublishedBy { get; set; }

    /// <summary>Why it was published, or where its figures came from.</summary>
    public required string Note { get; set; }

    /// <summary>Its prices, one per item.</summary>
    public ICollection<UnitPrice> Prices { get; } = [];
}

/// <summary>
/// One item's price in a price version: <see cref="Price"/> for every <see cref="Per"/> units of it, such as $0.10 for
/// 3,600 browser-seconds. Kept in the unit it is quoted in, so that no price is rounded on its way in.
/// </summary>
public sealed class UnitPrice : Entity, IAppendOnly
{
    /// <summary>The version it belongs to.</summary>
    public Guid PriceVersionId { get; set; }

    /// <summary>What is priced: one of <see cref="CostItems"/>.</summary>
    public required string Item { get; set; }

    /// <summary>What <see cref="Per"/> units cost.</summary>
    public decimal Price { get; set; }

    /// <summary>How many units the price is for.</summary>
    public long Per { get; set; }

    /// <summary>Whether the figure is an estimate rather than a price someone actually charges us.</summary>
    public bool Estimate { get; set; }

    /// <summary>The cost of a quantity at this price, to a millionth of the currency.</summary>
    public decimal CostOf(long quantity) => Math.Round(quantity * Price / Per, 6, MidpointRounding.AwayFromZero);
}
