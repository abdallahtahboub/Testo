namespace Autestiq.Domain.Costs;

/// <summary>
/// The price version the product shipped with, by fixed id, so the migration that seeds it and the code that refers to
/// it agree (ADR-0029). The machine and storage figures are estimates chosen by the owner on 2026-11-16, until hosting is
/// chosen; the model prices are Anthropic's list prices as checked on 2026-11-16.
/// </summary>
public static class PriceCatalog
{
    /// <summary>When the first version took effect.</summary>
    public static readonly DateTimeOffset Launched = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The currency every price is kept in.</summary>
    public const string Currency = "USD";

    public static readonly Guid FirstVersionId = new("01a1e500-0000-7000-8000-000000000201");

    /// <summary>Seconds in an hour: machine time is quoted per hour.</summary>
    public const long SecondsPerHour = 3_600;

    /// <summary>Bytes in a gigabyte as cloud storage counts them (GiB).</summary>
    public const long BytesPerGigabyte = 1_073_741_824;

    /// <summary>Tokens in a million: model prices are quoted per million tokens.</summary>
    public const long TokensPerMillion = 1_000_000;

    /// <summary>The first version.</summary>
    public static PriceVersion FirstVersion { get; } = new()
    {
        Id = FirstVersionId,
        Number = 1,
        EffectiveFrom = Launched,
        Currency = Currency,
        PublishedBy = "seed",
        Note = "Machine and storage are estimates until hosting is chosen; model prices are Anthropic's list prices on 2026-11-16.",
        CreatedAt = Launched,
        UpdatedAt = Launched,
    };

    /// <summary>The first version's prices.</summary>
    public static IReadOnlyList<UnitPrice> FirstPrices { get; } =
    [
        Price(1, CostItems.BrowserSecond, 0.10m, SecondsPerHour, estimate: true),
        Price(2, CostItems.StorageByteMonth, 0.023m, BytesPerGigabyte, estimate: true),
        Price(3, CostItems.ModelInputToken("claude-sonnet-5-5"), 2.00m, TokensPerMillion, estimate: false),
        Price(4, CostItems.ModelOutputToken("claude-sonnet-5-5"), 10.00m, TokensPerMillion, estimate: false),
        Price(5, CostItems.ModelInputToken("claude-opus-5-5"), 4.00m, TokensPerMillion, estimate: false),
        Price(6, CostItems.ModelOutputToken("claude-opus-5-5"), 20.00m, TokensPerMillion, estimate: false),
        Price(7, CostItems.ModelInputToken("claude-haiku-5-5"), 0.10m, TokensPerMillion, estimate: false),
        Price(8, CostItems.ModelOutputToken("claude-haiku-5-5"), 0.50m, TokensPerMillion, estimate: false),
    ];

    private static UnitPrice Price(int number, string item, decimal price, long per, bool estimate) => new()
    {
        Id = new Guid($"01a1e500-0000-7000-8000-0000000002{number + 10:00}"),
        PriceVersionId = FirstVersionId,
        Item = item,
        Price = price,
        Per = per,
        Estimate = estimate,
        CreatedAt = Launched,
        UpdatedAt = Launched,
    };
}
