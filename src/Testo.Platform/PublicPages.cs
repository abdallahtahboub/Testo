namespace Testo.Platform;

/// <summary>
/// Every page of the public website, as a path without its language prefix.
/// </summary>
/// <remarks>
/// <para>
/// One list, read by the pages themselves, the menu, the footer, <c>sitemap.xml</c> and the tests.
/// A page that exists and is not here has no sitemap entry and no smoke test, and a path that is
/// here and has no page fails a test — which is the only way a site of this size stays honest about
/// what it actually serves.
/// </para>
/// <para>
/// Paths stay English in both languages (<c>/de/pricing</c>, not <c>/de/preise</c>), as ADR-0006
/// decided. Translated slugs remain possible later because every page declares its own route per
/// language.
/// </para>
/// </remarks>
public static class PublicPages
{
    /// <summary>The home page, which is the language prefix on its own.</summary>
    public const string Home = "";

    /// <summary>The platform at a glance.</summary>
    public const string Product = "product";

    /// <summary>Test planning.</summary>
    public const string ProductPlanning = "product/planning";

    /// <summary>Test analysis and traceability.</summary>
    public const string ProductAnalysis = "product/analysis";

    /// <summary>Test design.</summary>
    public const string ProductDesign = "product/design";

    /// <summary>Test execution.</summary>
    public const string ProductExecution = "product/execution";

    /// <summary>Defect management.</summary>
    public const string ProductDefects = "product/defects";

    /// <summary>Reporting and release readiness.</summary>
    public const string ProductReporting = "product/reporting";

    /// <summary>What a capability is, and which ones exist.</summary>
    public const string ProductCapabilities = "product/capabilities";

    /// <summary>The web end-to-end capability, which is the one that is real.</summary>
    public const string ProductCapabilityE2EWeb = "product/capabilities/e2e-web";

    /// <summary>Integrations.</summary>
    public const string ProductIntegrations = "product/integrations";

    /// <summary>Security and trust.</summary>
    public const string ProductSecurity = "product/security";

    /// <summary>Where the solutions pages start.</summary>
    public const string Solutions = "solutions";

    /// <summary>For QA leads.</summary>
    public const string SolutionsQaLead = "solutions/qa-lead";

    /// <summary>For developers.</summary>
    public const string SolutionsDeveloper = "solutions/developer";

    /// <summary>For engineering managers.</summary>
    public const string SolutionsEngineeringManager = "solutions/engineering-manager";

    /// <summary>What the product costs.</summary>
    public const string Pricing = "pricing";

    /// <summary>Where the reading material starts.</summary>
    public const string Resources = "resources";

    /// <summary>The blog's list of articles.</summary>
    public const string Blog = "resources/blog";

    /// <summary>The documentation's table of contents.</summary>
    public const string Docs = "resources/docs";

    /// <summary>The case studies.</summary>
    public const string CaseStudies = "resources/case-studies";

    /// <summary>Who we are.</summary>
    public const string About = "about";

    /// <summary>Working here.</summary>
    public const string Careers = "careers";

    /// <summary>Getting in touch.</summary>
    public const string Contact = "contact";

    /// <summary>Asking for a demonstration.</summary>
    public const string RequestDemo = "request-demo";

    /// <summary>Who runs this website, as German law requires.</summary>
    public const string Imprint = "imprint";

    /// <summary>What we do with personal data.</summary>
    public const string Privacy = "privacy";

    /// <summary>The terms.</summary>
    public const string Terms = "terms";

    /// <summary>What we commit to, and how to tell us we have failed.</summary>
    public const string Accessibility = "accessibility";

    /// <summary>The industries a solutions page exists for. Placeholders, and labeled as such.</summary>
    public static IReadOnlyList<string> Industries { get; } =
        ["finance", "insurance", "retail", "automotive", "public-sector"];

    /// <summary>The path of one industry page.</summary>
    public static string Industry(string industry) => $"solutions/industries/{industry}";

    /// <summary>
    /// Every page, in the order a person would meet them. Industry pages are included, because a
    /// placeholder page that search engines can reach is still a page we are serving.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Home,
        Product,
        ProductPlanning,
        ProductAnalysis,
        ProductDesign,
        ProductExecution,
        ProductDefects,
        ProductReporting,
        ProductCapabilities,
        ProductCapabilityE2EWeb,
        ProductIntegrations,
        ProductSecurity,
        Solutions,
        SolutionsQaLead,
        SolutionsDeveloper,
        SolutionsEngineeringManager,
        .. Industries.Select(Industry),
        Pricing,
        Resources,
        Blog,
        Docs,
        CaseStudies,
        About,
        Careers,
        Contact,
        RequestDemo,
        Imprint,
        Privacy,
        Terms,
        Accessibility,
    ];

    /// <summary>
    /// The pages that carry a form, and therefore need <c>[HasForm]</c> to get an antiforgery
    /// token on a statically rendered page (ADR-0009). A test holds the pages to this list.
    /// </summary>
    public static IReadOnlyList<string> WithForms { get; } = [Contact, RequestDemo];

    /// <summary>
    /// The pages that ask search engines to leave them alone.
    /// </summary>
    /// <remarks>
    /// All of them are placeholders. A placeholder that ranks is a placeholder a stranger reads
    /// first, and the first impression of this product should not be an empty page — so these are
    /// served with <c>noindex</c> and left out of <c>sitemap.xml</c>. A test holds the pages
    /// themselves to this list, because two descriptions of the same thing always drift.
    /// </remarks>
    public static IReadOnlyList<string> NotIndexed { get; } =
    [
        ProductIntegrations,
        .. Industries.Select(Industry),
        Blog,
        Docs,
        CaseStudies,
        Careers,
        Imprint,
        Privacy,
        Terms,
    ];

    /// <summary>The pages that belong in <c>sitemap.xml</c>: everything that is actually written.</summary>
    public static IReadOnlyList<string> Indexed { get; } =
        [.. All.Where(page => !NotIndexed.Contains(page, StringComparer.Ordinal))];

    /// <summary>
    /// How important a page is, for <c>sitemap.xml</c>. Not a ranking promise — a hint, and a way
    /// of saying out loud which pages we think matter.
    /// </summary>
    public static double Priority(string page) => page switch
    {
        Home => 1.0,
        Product or Pricing => 0.9,
        ProductCapabilityE2EWeb or Solutions or RequestDemo => 0.8,
        Imprint or Privacy or Terms or Accessibility => 0.3,
        _ when page.StartsWith("solutions/industries/", StringComparison.Ordinal) => 0.4,
        _ => 0.6,
    };
}
