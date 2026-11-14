using Testo.Platform;

namespace Testo.Website.Content;

/// <summary>What a page says about one stage of the testing journey.</summary>
/// <param name="Page">Its path, from <see cref="PublicPages"/>.</param>
/// <param name="OverlineKey">The small label above the heading.</param>
/// <param name="TitleKey">The heading.</param>
/// <param name="LeadKey">One or two sentences saying what the stage is for.</param>
/// <param name="WhyKey">Why it is usually done badly, or not at all.</param>
/// <param name="PointsHeadingKey">The heading over the cards.</param>
/// <param name="Points">What the stage covers, as cards.</param>
/// <param name="TodayKey">
/// What exists today, in this product, for this stage. Said on every page, because a website that
/// describes six stages as though all six were finished is a website that lies.
/// </param>
/// <param name="IsBuilt">Whether any of it is built. Decides which of the two "today" notes is shown.</param>
public sealed record StagePage(
    string Page,
    string OverlineKey,
    string TitleKey,
    string LeadKey,
    string WhyKey,
    string PointsHeadingKey,
    IReadOnlyList<StagePoint> Points,
    string TodayKey,
    bool IsBuilt);

/// <summary>One thing a stage covers.</summary>
/// <param name="TitleKey">Its name.</param>
/// <param name="TextKey">What it means.</param>
public sealed record StagePoint(string TitleKey, string TextKey);

/// <summary>
/// The six stages of the ISTQB testing journey, as the website describes them.
/// </summary>
/// <remarks>
/// <para>
/// The copy lives in the resource files; this holds the structure and the keys. Every key is
/// written out in full rather than built from the stage's name, so that the resource tests can find
/// it and a translator can look it up — the same reason the run statuses are spelled out.
/// </para>
/// <para>
/// Each stage says what exists today. Only execution does, and the pages for the other five say so
/// plainly instead of describing a plan in the present tense. A marketing site is still a statement
/// of fact about a product.
/// </para>
/// </remarks>
public static class JourneyStages
{
    /// <summary>Test planning.</summary>
    public static StagePage Planning { get; } = new(
        PublicPages.ProductPlanning,
        "Stage_Planning_Overline",
        "Stage_Planning_Title",
        "Stage_Planning_Lead",
        "Stage_Planning_Why",
        "Stage_Planning_Points",
        [
            new("Stage_Planning_Point1_Title", "Stage_Planning_Point1_Text"),
            new("Stage_Planning_Point2_Title", "Stage_Planning_Point2_Text"),
            new("Stage_Planning_Point3_Title", "Stage_Planning_Point3_Text"),
        ],
        "Stage_Planning_Today",
        IsBuilt: false);

    /// <summary>Test analysis and traceability.</summary>
    public static StagePage Analysis { get; } = new(
        PublicPages.ProductAnalysis,
        "Stage_Analysis_Overline",
        "Stage_Analysis_Title",
        "Stage_Analysis_Lead",
        "Stage_Analysis_Why",
        "Stage_Analysis_Points",
        [
            new("Stage_Analysis_Point1_Title", "Stage_Analysis_Point1_Text"),
            new("Stage_Analysis_Point2_Title", "Stage_Analysis_Point2_Text"),
            new("Stage_Analysis_Point3_Title", "Stage_Analysis_Point3_Text"),
        ],
        "Stage_Analysis_Today",
        IsBuilt: false);

    /// <summary>Test design.</summary>
    public static StagePage Design { get; } = new(
        PublicPages.ProductDesign,
        "Stage_Design_Overline",
        "Stage_Design_Title",
        "Stage_Design_Lead",
        "Stage_Design_Why",
        "Stage_Design_Points",
        [
            new("Stage_Design_Point1_Title", "Stage_Design_Point1_Text"),
            new("Stage_Design_Point2_Title", "Stage_Design_Point2_Text"),
            new("Stage_Design_Point3_Title", "Stage_Design_Point3_Text"),
        ],
        "Stage_Design_Today",
        IsBuilt: false);

    /// <summary>Test execution. The one that is real.</summary>
    public static StagePage Execution { get; } = new(
        PublicPages.ProductExecution,
        "Stage_Execution_Overline",
        "Stage_Execution_Title",
        "Stage_Execution_Lead",
        "Stage_Execution_Why",
        "Stage_Execution_Points",
        [
            new("Stage_Execution_Point1_Title", "Stage_Execution_Point1_Text"),
            new("Stage_Execution_Point2_Title", "Stage_Execution_Point2_Text"),
            new("Stage_Execution_Point3_Title", "Stage_Execution_Point3_Text"),
        ],
        "Stage_Execution_Today",
        IsBuilt: true);

    /// <summary>Defect management.</summary>
    public static StagePage Defects { get; } = new(
        PublicPages.ProductDefects,
        "Stage_Defects_Overline",
        "Stage_Defects_Title",
        "Stage_Defects_Lead",
        "Stage_Defects_Why",
        "Stage_Defects_Points",
        [
            new("Stage_Defects_Point1_Title", "Stage_Defects_Point1_Text"),
            new("Stage_Defects_Point2_Title", "Stage_Defects_Point2_Text"),
            new("Stage_Defects_Point3_Title", "Stage_Defects_Point3_Text"),
        ],
        "Stage_Defects_Today",
        IsBuilt: false);

    /// <summary>Reporting and release readiness.</summary>
    public static StagePage Reporting { get; } = new(
        PublicPages.ProductReporting,
        "Stage_Reporting_Overline",
        "Stage_Reporting_Title",
        "Stage_Reporting_Lead",
        "Stage_Reporting_Why",
        "Stage_Reporting_Points",
        [
            new("Stage_Reporting_Point1_Title", "Stage_Reporting_Point1_Text"),
            new("Stage_Reporting_Point2_Title", "Stage_Reporting_Point2_Text"),
            new("Stage_Reporting_Point3_Title", "Stage_Reporting_Point3_Text"),
        ],
        "Stage_Reporting_Today",
        IsBuilt: false);

    /// <summary>All six, in the order the work happens.</summary>
    public static IReadOnlyList<StagePage> All { get; } =
        [Planning, Analysis, Design, Execution, Defects, Reporting];

    /// <summary>The stage before this one, for the link at the foot of a stage page.</summary>
    public static StagePage? Previous(StagePage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var index = All.ToList().FindIndex(candidate => candidate.Page == stage.Page);
        return index > 0 ? All[index - 1] : null;
    }

    /// <summary>The stage after this one.</summary>
    public static StagePage? Next(StagePage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var index = All.ToList().FindIndex(candidate => candidate.Page == stage.Page);
        return index >= 0 && index < All.Count - 1 ? All[index + 1] : null;
    }
}
