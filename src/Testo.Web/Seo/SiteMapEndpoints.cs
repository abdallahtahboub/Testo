using System.Text;
using System.Xml;
using Testo.Platform;
using Testo.Platform.Localization;

namespace Testo.Web.Seo;

/// <summary>
/// Serves <c>sitemap.xml</c> and <c>robots.txt</c>.
/// </summary>
/// <remarks>
/// Both are generated from <see cref="PublicPages"/> rather than written by hand, so they cannot
/// describe a site we do not serve. A hand-maintained sitemap is wrong within a month: it is the
/// file nobody opens, listing the pages somebody moved.
/// </remarks>
public static class SiteMapEndpoints
{
    /// <summary>Adds both files.</summary>
    public static IEndpointRouteBuilder MapSiteMap(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/sitemap.xml", (HttpContext context) =>
            Results.Content(SiteMap(Origin(context)), "application/xml; charset=utf-8"))
            .AllowAnonymous();

        endpoints.MapGet("/robots.txt", (HttpContext context) =>
            Results.Text(Robots(Origin(context)), "text/plain; charset=utf-8"))
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Every written page, once per language, each entry naming the other languages as
    /// alternates — which is what tells a search engine these are the same page rather than two
    /// pages competing with each other (ADR-0006).
    /// </summary>
    public static string SiteMap(string origin)
    {
        var text = new StringBuilder();

        using var writer = XmlWriter.Create(text, new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
        writer.WriteAttributeString("xmlns", "xhtml", null, "http://www.w3.org/1999/xhtml");

        foreach (var page in PublicPages.Indexed)
        {
            foreach (var culture in SupportedCultures.All)
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", origin + LocalizedPath.For(culture, page));

                foreach (var alternate in SupportedCultures.All)
                {
                    writer.WriteStartElement("link", "http://www.w3.org/1999/xhtml");
                    writer.WriteAttributeString("rel", "alternate");
                    writer.WriteAttributeString("hreflang", alternate);
                    writer.WriteAttributeString("href", origin + LocalizedPath.For(alternate, page));
                    writer.WriteEndElement();
                }

                writer.WriteElementString(
                    "priority",
                    PublicPages.Priority(page).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));

                writer.WriteEndElement();
            }
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        return text.ToString();
    }

    /// <summary>
    /// What a crawler may look at.
    /// </summary>
    /// <remarks>
    /// The product and the account pages are disallowed: they need a session, so a crawler reaching
    /// them gets a redirect to sign-in, and a thousand of those is noise for everybody. This is
    /// politeness, not protection — the authorization gate is what protects them (D-1).
    /// </remarks>
    public static string Robots(string origin) =>
        $"""
        User-agent: *
        Disallow: {Routes.App}
        Disallow: {Routes.Account}
        Allow: /

        Sitemap: {origin}/sitemap.xml
        """;

    private static string Origin(HttpContext context) =>
        $"{context.Request.Scheme}://{context.Request.Host}";
}
