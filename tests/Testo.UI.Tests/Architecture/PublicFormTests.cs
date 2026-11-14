using System.Text.RegularExpressions;
using Testo.Platform;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// A public page is rendered statically and gets no antiforgery token unless it asks for one
/// (ADR-0009). A page with a form that forgot <c>[HasForm]</c> looks perfectly correct and fails
/// only when somebody presses the button, with a 400 and no explanation — exactly the kind of fault
/// a test should find instead of a visitor.
/// </summary>
public partial class PublicFormTests
{
    /// <summary>
    /// The attribute itself, on a line of its own.
    /// </summary>
    /// <remarks>
    /// Not a substring search for "[HasForm]", which is how the first version of this test was
    /// written and why it was useless: the contact page's own comment explains why it carries the
    /// attribute, so the search found the explanation and passed with the attribute deleted.
    /// Caught by removing the attribute and watching the test stay green.
    /// </remarks>
    [GeneratedRegex(@"^\s*@attribute\s*\[\s*HasForm\s*\]\s*$", RegexOptions.Multiline)]
    private static partial Regex HasFormAttribute();

    /// <summary>Every routable page of the public website, as a repository-relative path.</summary>
    private static IReadOnlyList<string> Files { get; } =
    [
        .. RepositoryRoot.SourceFiles("*.razor")
            .Where(file => RepositoryRoot.Relative(file).StartsWith("src/Testo.Website/Pages/", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("@page", StringComparison.Ordinal))
            .Select(RepositoryRoot.Relative)
            .Order(StringComparer.Ordinal),
    ];

    public static TheoryData<string> PublicPageFiles => [.. Files];

    [Fact]
    public void ThereArePublicPagesToCheck()
    {
        // Without this the theory below would pass by having nothing to say.
        Assert.NotEmpty(PublicPageFiles);
    }

    [Theory]
    [MemberData(nameof(PublicPageFiles))]
    public void PublicPageWithAForm_DeclaresHasForm(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, file));

        var hasForm = text.Contains("<EditForm", StringComparison.Ordinal)
            || text.Contains("<form", StringComparison.Ordinal);

        if (!hasForm)
        {
            return;
        }

        Assert.True(
            HasFormAttribute().IsMatch(text),
            $"{file} has a form and no [HasForm] attribute, so its post will be answered with 400.");
    }

    [Theory]
    [MemberData(nameof(PublicPageFiles))]
    public void PublicPageWithoutAForm_DoesNotAskForATokenItDoesNotNeed(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, file));

        if (text.Contains("<EditForm", StringComparison.Ordinal) || text.Contains("<form", StringComparison.Ordinal))
        {
            return;
        }

        Assert.False(
            HasFormAttribute().IsMatch(text),
            $"{file} declares [HasForm] and has no form. Either the form was removed and the attribute "
            + "was left behind, or the attribute was copied from another page.");
    }

    [Fact]
    public void ThePagesWithFormsAreTheOnesTheCatalogueSaysTheyAre()
    {
        // PublicPages.WithForms is what the sitemap and the smoke tests read. If a form is added to
        // a page and that list is not updated, the two descriptions of the site disagree, and this
        // is where that is noticed.
        var declared = Files
            .Where(file => HasFormAttribute().IsMatch(File.ReadAllText(Path.Combine(RepositoryRoot.Path, file))))
            .Select(RouteOf)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PublicPages.WithForms.Order(StringComparer.Ordinal), declared);
    }

    /// <summary>
    /// The page path a file serves, taken from its English route, without the language prefix.
    /// </summary>
    private static string RouteOf(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, file));
        var marker = text.IndexOf("@page \"/en", StringComparison.Ordinal);

        Assert.True(marker >= 0, $"{file} has no English route.");

        var start = marker + "@page \"".Length;
        var end = text.IndexOf('"', start);

        return text[start..end].TrimStart('/')["en".Length..].Trim('/');
    }
}
