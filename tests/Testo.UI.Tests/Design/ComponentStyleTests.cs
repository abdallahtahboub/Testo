using System.Text.RegularExpressions;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Design;

/// <summary>
/// Guards the rule that component styles use design tokens: no raw colors and no pixel
/// values other than 0 and 1px (<c>CLAUDE.md</c>, CSS).
/// </summary>
public partial class ComponentStyleTests
{
    /// <summary>
    /// Component styles, plus the shared stylesheets. Not <c>tokens.css</c>, which is where
    /// the raw values are allowed to live, and not <c>fluent-bridge.css</c>, which only maps
    /// one set of names onto another.
    /// </summary>
    public static TheoryData<string> StyleSheets =>
    [
        .. RepositoryRoot.SourceFiles("*.razor.css")
            .Concat(RepositoryRoot.SourceFiles("*.css").Where(IsSharedStyleSheet))
            .Order(StringComparer.Ordinal)
            .Select(RepositoryRoot.Relative),
    ];

    private static bool IsSharedStyleSheet(string file)
    {
        var name = Path.GetFileName(file);
        return name is "base.css" or "controls.css";
    }

    private static string Read(string relativePath) =>
        Comment().Replace(File.ReadAllText(Path.Combine(RepositoryRoot.Path, relativePath)), "");

    [Fact]
    public void StyleSheets_Exist()
    {
        Assert.NotEmpty(StyleSheets);
    }

    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void StyleSheet_Colors_ComeFromTokens(string file)
    {
        Assert.Empty(RawColor().Matches(Read(file)).Select(match => match.Value).Distinct());
    }

    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void StyleSheet_Sizes_AreNotInPixels(string file)
    {
        var pixels = PixelValue().Matches(Read(file))
            .Where(match => match.Groups[1].Value is not ("0" or "1"))
            .Select(match => match.Value)
            .Distinct();

        Assert.Empty(pixels);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b|\b(?:rgba?|hsla?|oklch|oklab|color)\(")]
    private static partial Regex RawColor();

    [GeneratedRegex(@"(?<![\w.-])(\d*\.?\d+)px\b")]
    private static partial Regex PixelValue();
}
