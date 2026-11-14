using Testo.Platform;
using Testo.Platform.Localization;
using Testo.Platform.Navigation;
using Testo.Website.Resources;

namespace Testo.Website;

/// <summary>
/// The public site's menu and footer.
/// </summary>
/// <remarks>
/// <para>
/// Registered with the chrome rather than written into it, so that the design system renders a
/// menu whose words it does not own. Every label names this project's resource file, and the
/// header looks it up in the visitor's language as it draws.
/// </para>
/// <para>
/// Each top-level section's panel opens with a link to the section's own page. A mega-menu that
/// can only be used by hovering is no use on a phone or to anybody navigating by keyboard, so
/// "Overview" is always the first thing in the panel and the section always has a page behind it.
/// </para>
/// </remarks>
public sealed class SiteMenuCatalogue : ISiteMenu
{
    /// <inheritdoc />
    public IReadOnlyList<SiteMenuSection> Sections { get; } =
    [
        new(
            "product",
            Text("Nav_Product"),
            [
                new SiteLinkGroup(
                    Text("Nav_Product_Platform"),
                    [
                        Link("Nav_Product_Overview", PublicPages.Product, "Nav_Product_Overview_Text"),
                        Link("Nav_Capabilities", PublicPages.ProductCapabilities, "Nav_Capabilities_Text"),
                        Link("Nav_Integrations", PublicPages.ProductIntegrations, "Nav_Integrations_Text", placeholder: true),
                        Link("Nav_Security", PublicPages.ProductSecurity, "Nav_Security_Text"),
                    ]),
                new SiteLinkGroup(
                    Text("Nav_Product_Journey"),
                    [
                        Link("Nav_Planning", PublicPages.ProductPlanning, "Nav_Planning_Text"),
                        Link("Nav_Analysis", PublicPages.ProductAnalysis, "Nav_Analysis_Text"),
                        Link("Nav_Design", PublicPages.ProductDesign, "Nav_Design_Text"),
                        Link("Nav_Execution", PublicPages.ProductExecution, "Nav_Execution_Text"),
                        Link("Nav_Defects", PublicPages.ProductDefects, "Nav_Defects_Text"),
                        Link("Nav_Reporting", PublicPages.ProductReporting, "Nav_Reporting_Text"),
                    ]),
            ],
            new SiteFeature(
                Text("Nav_Feature_Run_Title"),
                Text("Nav_Feature_Run_Text"),
                PublicPages.ProductCapabilityE2EWeb,
                Text("Nav_Feature_Run_Action")),
            PublicPages.Product),

        new(
            "solutions",
            Text("Nav_Solutions"),
            [
                new SiteLinkGroup(
                    Text("Nav_Solutions_ByRole"),
                    [
                        Link("Nav_Solutions_Overview", PublicPages.Solutions),
                        Link("Nav_QaLead", PublicPages.SolutionsQaLead, "Nav_QaLead_Text"),
                        Link("Nav_Developer", PublicPages.SolutionsDeveloper, "Nav_Developer_Text"),
                        Link("Nav_EngineeringManager", PublicPages.SolutionsEngineeringManager, "Nav_EngineeringManager_Text"),
                    ]),
                new SiteLinkGroup(
                    Text("Nav_Solutions_ByIndustry"),
                    [
                        Link("Nav_Industry_Finance", PublicPages.Industry("finance"), placeholder: true),
                        Link("Nav_Industry_Insurance", PublicPages.Industry("insurance"), placeholder: true),
                        Link("Nav_Industry_Retail", PublicPages.Industry("retail"), placeholder: true),
                        Link("Nav_Industry_Automotive", PublicPages.Industry("automotive"), placeholder: true),
                        Link("Nav_Industry_PublicSector", PublicPages.Industry("public-sector"), placeholder: true),
                    ]),
            ],
            new SiteFeature(
                Text("Nav_Feature_Traceability_Title"),
                Text("Nav_Feature_Traceability_Text"),
                PublicPages.ProductAnalysis,
                Text("Nav_Feature_Traceability_Action")),
            PublicPages.Solutions),

        new(
            "resources",
            Text("Nav_Resources"),
            [
                new SiteLinkGroup(
                    Text("Nav_Resources_Read"),
                    [
                        Link("Nav_Resources_Overview", PublicPages.Resources),
                        Link("Nav_Blog", PublicPages.Blog, "Nav_Blog_Text", placeholder: true),
                        Link("Nav_Docs", PublicPages.Docs, "Nav_Docs_Text", placeholder: true),
                        Link("Nav_CaseStudies", PublicPages.CaseStudies, "Nav_CaseStudies_Text", placeholder: true),
                    ]),
            ],
            new SiteFeature(
                Text("Nav_Feature_Accessibility_Title"),
                Text("Nav_Feature_Accessibility_Text"),
                PublicPages.Accessibility,
                Text("Nav_Feature_Accessibility_Action")),
            PublicPages.Resources),

        new(
            "company",
            Text("Nav_Company"),
            [
                new SiteLinkGroup(
                    null,
                    [
                        Link("Nav_About", PublicPages.About, "Nav_About_Text"),
                        Link("Nav_Careers", PublicPages.Careers, "Nav_Careers_Text", placeholder: true),
                        Link("Nav_Contact", PublicPages.Contact, "Nav_Contact_Text"),
                    ]),
            ],
            new SiteFeature(
                Text("Nav_Feature_Demo_Title"),
                Text("Nav_Feature_Demo_Text"),
                PublicPages.RequestDemo,
                Text("Nav_Feature_Demo_Action")),
            PublicPages.About),
    ];

    /// <inheritdoc />
    public IReadOnlyList<SiteLink> HeaderLinks { get; } =
        [Link("Nav_Pricing", PublicPages.Pricing, placeholder: true)];

    /// <inheritdoc />
    public IReadOnlyList<SiteLinkGroup> FooterGroups { get; } =
    [
        new(
            Text("Nav_Product"),
            [
                Link("Nav_Product_Overview", PublicPages.Product),
                Link("Nav_Capabilities", PublicPages.ProductCapabilities),
                Link("Nav_Execution", PublicPages.ProductExecution),
                Link("Nav_Integrations", PublicPages.ProductIntegrations, placeholder: true),
                Link("Nav_Security", PublicPages.ProductSecurity),
                Link("Nav_Pricing", PublicPages.Pricing, placeholder: true),
            ]),
        new(
            Text("Nav_Solutions"),
            [
                Link("Nav_QaLead", PublicPages.SolutionsQaLead),
                Link("Nav_Developer", PublicPages.SolutionsDeveloper),
                Link("Nav_EngineeringManager", PublicPages.SolutionsEngineeringManager),
            ]),
        new(
            Text("Nav_Resources"),
            [
                Link("Nav_Resources_Overview", PublicPages.Resources),
                Link("Nav_Blog", PublicPages.Blog, placeholder: true),
                Link("Nav_Docs", PublicPages.Docs, placeholder: true),
                Link("Nav_CaseStudies", PublicPages.CaseStudies, placeholder: true),
            ]),
        new(
            Text("Nav_Company"),
            [
                Link("Nav_About", PublicPages.About),
                Link("Nav_Careers", PublicPages.Careers, placeholder: true),
                Link("Nav_Contact", PublicPages.Contact),
                Link("Nav_RequestDemo", PublicPages.RequestDemo),
            ]),
    ];

    /// <inheritdoc />
    public IReadOnlyList<SiteLink> LegalLinks { get; } =
    [
        Link("Nav_Imprint", PublicPages.Imprint, placeholder: true),
        Link("Nav_Privacy", PublicPages.Privacy, placeholder: true),
        Link("Nav_Terms", PublicPages.Terms, placeholder: true),
        Link("Nav_Accessibility", PublicPages.Accessibility),
    ];

    private static LocalizedText Text(string key) => LocalizedText.From<WebsiteResources>(key);

    private static SiteLink Link(string labelKey, string page, string? descriptionKey = null, bool placeholder = false) =>
        new(Text(labelKey), page, descriptionKey is null ? null : Text(descriptionKey), placeholder);
}
