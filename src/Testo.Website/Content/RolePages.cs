using Testo.Platform;

namespace Testo.Website.Content;

/// <summary>What a page says to one role.</summary>
/// <param name="Page">Its path.</param>
/// <param name="TitleKey">The heading.</param>
/// <param name="LeadKey">Who this is for, in their own terms.</param>
/// <param name="DayKey">What the day actually looks like for them now.</param>
/// <param name="PointsHeadingKey">The heading over the cards.</param>
/// <param name="Points">What the product would change, as cards.</param>
/// <param name="TodayKey">Which of that is real today.</param>
public sealed record RolePage(
    string Page,
    string TitleKey,
    string LeadKey,
    string DayKey,
    string PointsHeadingKey,
    IReadOnlyList<StagePoint> Points,
    string TodayKey);

/// <summary>
/// The three roles the product is pitched to.
/// </summary>
/// <remarks>
/// Each one starts from the day that role actually has, rather than from a feature list. A page
/// that opens with what we built is a page about us; a page that opens with their Tuesday is a
/// page they finish reading.
/// </remarks>
public static class RolePages
{
    /// <summary>The person answerable for whether testing was enough.</summary>
    public static RolePage QaLead { get; } = new(
        PublicPages.SolutionsQaLead,
        "Role_QaLead_Title",
        "Role_QaLead_Lead",
        "Role_QaLead_Day",
        "Role_QaLead_Points",
        [
            new("Role_QaLead_Point1_Title", "Role_QaLead_Point1_Text"),
            new("Role_QaLead_Point2_Title", "Role_QaLead_Point2_Text"),
            new("Role_QaLead_Point3_Title", "Role_QaLead_Point3_Text"),
        ],
        "Role_QaLead_Today");

    /// <summary>The person who has to fix what the testing found.</summary>
    public static RolePage Developer { get; } = new(
        PublicPages.SolutionsDeveloper,
        "Role_Developer_Title",
        "Role_Developer_Lead",
        "Role_Developer_Day",
        "Role_Developer_Points",
        [
            new("Role_Developer_Point1_Title", "Role_Developer_Point1_Text"),
            new("Role_Developer_Point2_Title", "Role_Developer_Point2_Text"),
            new("Role_Developer_Point3_Title", "Role_Developer_Point3_Text"),
        ],
        "Role_Developer_Today");

    /// <summary>The person who has to report on quality without having watched it.</summary>
    public static RolePage EngineeringManager { get; } = new(
        PublicPages.SolutionsEngineeringManager,
        "Role_Manager_Title",
        "Role_Manager_Lead",
        "Role_Manager_Day",
        "Role_Manager_Points",
        [
            new("Role_Manager_Point1_Title", "Role_Manager_Point1_Text"),
            new("Role_Manager_Point2_Title", "Role_Manager_Point2_Text"),
            new("Role_Manager_Point3_Title", "Role_Manager_Point3_Text"),
        ],
        "Role_Manager_Today");

    /// <summary>All three.</summary>
    public static IReadOnlyList<RolePage> All { get; } = [QaLead, Developer, EngineeringManager];
}
