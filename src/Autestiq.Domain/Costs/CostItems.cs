namespace Autestiq.Domain.Costs;

/// <summary>The things a run's cost is made of, as they are named in a price version and on a cost row.</summary>
public static class CostItems
{
    /// <summary>A second of a browser's machine time.</summary>
    public const string BrowserSecond = "browser-second";

    /// <summary>A byte of evidence kept for a month.</summary>
    public const string StorageByteMonth = "storage-byte-month";

    /// <summary>A model input token, for one model.</summary>
    public static string ModelInputToken(string model) => "model-input-token:" + model;

    /// <summary>A model output token, for one model.</summary>
    public static string ModelOutputToken(string model) => "model-output-token:" + model;
}
