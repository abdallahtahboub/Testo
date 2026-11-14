using Testo.Platform.Localization;

namespace Testo.Platform.Navigation;

/// <summary>One link in the public site's menu or footer.</summary>
/// <param name="Label">What it says, from the resources of whoever registered it.</param>
/// <param name="Page">A page path from <see cref="PublicPages"/>, without its language prefix.</param>
/// <param name="Description">
/// One line under the label in a mega-menu. A menu of bare nouns makes a visitor guess.
/// </param>
/// <param name="IsPlaceholder">
/// Whether the page it leads to is a labeled placeholder. Shown, because a visitor who follows a
/// link to an empty page should have been told.
/// </param>
public sealed record SiteLink(
    LocalizedText Label,
    string Page,
    LocalizedText? Description = null,
    bool IsPlaceholder = false);

/// <summary>One column of a mega-menu or of the footer.</summary>
/// <param name="Heading">What the column is called, or <c>null</c> for an unheaded list.</param>
/// <param name="Links">The links, in order.</param>
public sealed record SiteLinkGroup(LocalizedText? Heading, IReadOnlyList<SiteLink> Links);

/// <summary>
/// The one highlighted card in a mega-menu: somewhere we would rather a visitor went.
/// </summary>
/// <param name="Title">Its heading.</param>
/// <param name="Text">Why it is worth following.</param>
/// <param name="Page">Where it leads.</param>
/// <param name="ActionLabel">What the link says.</param>
public sealed record SiteFeature(
    LocalizedText Title,
    LocalizedText Text,
    string Page,
    LocalizedText ActionLabel);

/// <summary>One top-level item of the site header: a word, and what drops down under it.</summary>
/// <param name="Id">A stable id, for the markup and for tests.</param>
/// <param name="Label">The word in the header.</param>
/// <param name="Groups">The columns of the panel.</param>
/// <param name="Feature">The highlighted card, when the panel has one.</param>
/// <param name="Page">
/// Where the top-level word itself leads. A mega-menu that cannot be used without hovering is
/// unusable by keyboard and on a phone, so every section also has a page of its own.
/// </param>
public sealed record SiteMenuSection(
    string Id,
    LocalizedText Label,
    IReadOnlyList<SiteLinkGroup> Groups,
    SiteFeature? Feature,
    string Page);

/// <summary>
/// The public site's navigation, as data.
/// </summary>
/// <remarks>
/// <para>
/// Registered rather than written into the layout, for the same reason the app's navigation is
/// (<c>CLAUDE.md</c>, Architecture rules): the chrome renders what it is given. It also keeps the
/// website's copy in the website's own resource files, so the design system holds no marketing
/// text — <see cref="LocalizedText"/> names the resource assembly, and the header looks the label
/// up in the visitor's language at the moment it draws it.
/// </para>
/// <para>
/// There is one of these, not a collection: a public website has a single menu, and pretending
/// otherwise would be ceremony.
/// </para>
/// </remarks>
public interface ISiteMenu
{
    /// <summary>The header's top-level sections, in order.</summary>
    IReadOnlyList<SiteMenuSection> Sections { get; }

    /// <summary>Links that sit beside the sections in the header, such as Pricing.</summary>
    IReadOnlyList<SiteLink> HeaderLinks { get; }

    /// <summary>The footer's columns.</summary>
    IReadOnlyList<SiteLinkGroup> FooterGroups { get; }

    /// <summary>The footer's legal row, kept apart because it is required rather than useful.</summary>
    IReadOnlyList<SiteLink> LegalLinks { get; }
}
