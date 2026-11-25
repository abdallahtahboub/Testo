using Autestiq.Domain.Common;

namespace Autestiq.Domain.Costs;

/// <summary>
/// What one item of one run cost us (Milestone 3.8, gap G10, ADR-0029): a quantity times the price in force when the
/// run finished, with the version that price came from. Append-only, and recorded once: its key names the run and the
/// item. Recorded whether or not the run is billable, because a run that fails by our fault still costs us.
/// </summary>
public sealed class RunCost : Entity, IBelongsToOrganization, IAppendOnly
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The run it is for.</summary>
    public Guid RunId { get; set; }

    /// <summary>What it is the cost of: one of <see cref="CostItems"/>.</summary>
    public required string Item { get; set; }

    /// <summary>How much of it, in the item's unit.</summary>
    public long Quantity { get; set; }

    /// <summary>The price applied, for <see cref="Per"/> units, copied from the price version.</summary>
    public decimal Price { get; set; }

    /// <summary>How many units the price is for.</summary>
    public long Per { get; set; }

    /// <summary>What it cost: the quantity at the price, to a millionth of the currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>The currency of the amount.</summary>
    public required string Currency { get; set; }

    /// <summary>The price version the price came from.</summary>
    public Guid PriceVersionId { get; set; }

    /// <summary>Whether the price was an estimate.</summary>
    public bool Estimate { get; set; }

    /// <summary>Whether the run is billable (decision B). A cost that is not is one we carry ourselves.</summary>
    public bool Billable { get; set; }

    /// <summary>The month it belongs to, <c>yyyy-MM</c> in UTC, like the usage it goes with.</summary>
    public required string Period { get; set; }

    /// <summary>What makes it one row however often the run's result is written back.</summary>
    public required string IdempotencyKey { get; set; }
}
