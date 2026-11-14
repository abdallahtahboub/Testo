using Testo.Platform;

namespace Testo.Website.Content;

/// <summary>An industry page. All of them are placeholders, and all of them say so.</summary>
/// <param name="Page">Its path.</param>
/// <param name="TitleKey">The industry's name.</param>
/// <param name="LeadKey">
/// The one thing about testing in that industry we are confident is true. Deliberately short: a
/// page of invented sector detail is the kind of thing a buyer checks and catches.
/// </param>
public sealed record IndustryPage(string Page, string TitleKey, string LeadKey);

/// <summary>
/// The five industries the sitemap calls for.
/// </summary>
/// <remarks>
/// Each page exists so the structure is real and the URLs are stable, and each carries a
/// placeholder banner and <c>noindex</c>. Writing five pages of sector platitudes we cannot support
/// would be worse than leaving them unwritten, which is why the copy here stops at what we can
/// actually stand behind.
/// </remarks>
public static class IndustryPages
{
    /// <summary>Banks, payments, anything regulated by a financial authority.</summary>
    public static IndustryPage Finance { get; } = new(PublicPages.Industry("finance"), "Industry_Finance_Title", "Industry_Finance_Lead");

    /// <summary>Insurers.</summary>
    public static IndustryPage Insurance { get; } = new(PublicPages.Industry("insurance"), "Industry_Insurance_Title", "Industry_Insurance_Lead");

    /// <summary>Shops, marketplaces, anything with a checkout.</summary>
    public static IndustryPage Retail { get; } = new(PublicPages.Industry("retail"), "Industry_Retail_Title", "Industry_Retail_Lead");

    /// <summary>Vehicle makers and their suppliers.</summary>
    public static IndustryPage Automotive { get; } = new(PublicPages.Industry("automotive"), "Industry_Automotive_Title", "Industry_Automotive_Lead");

    /// <summary>Government and public bodies.</summary>
    public static IndustryPage PublicSector { get; } = new(PublicPages.Industry("public-sector"), "Industry_PublicSector_Title", "Industry_PublicSector_Lead");

    /// <summary>All five.</summary>
    public static IReadOnlyList<IndustryPage> All { get; } = [Finance, Insurance, Retail, Automotive, PublicSector];
}
