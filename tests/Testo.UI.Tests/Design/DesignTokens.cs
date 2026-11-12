using System.Globalization;
using System.Text.RegularExpressions;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Design;

/// <summary>Reads <c>tokens.css</c> and resolves color tokens for the light and the dark theme.</summary>
internal sealed partial class DesignTokens
{
    private readonly Dictionary<string, string> _declarations;

    private DesignTokens(Dictionary<string, string> declarations) => _declarations = declarations;

    public static string FilePath { get; } =
        Path.Combine(RepositoryRoot.Source, "Testo.UI.Shared", "wwwroot", "css", "tokens.css");

    public static DesignTokens Current { get; } = Load();

    public IReadOnlyCollection<string> Names => _declarations.Keys;

    /// <summary>The color of a token as <c>#RRGGBB</c> in the given theme.</summary>
    public string Color(string token, bool dark)
    {
        Assert.True(_declarations.TryGetValue(token, out var value), $"tokens.css has no --{token}.");
        return Resolve(value, dark, token);
    }

    /// <summary>The WCAG contrast ratio between two tokens in the given theme.</summary>
    public double Contrast(string foreground, string background, bool dark)
    {
        var first = Luminance(Color(foreground, dark));
        var second = Luminance(Color(background, dark));
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private string Resolve(string value, bool dark, string token)
    {
        value = value.Trim();

        var reference = Reference().Match(value);
        if (reference.Success)
        {
            return Color(reference.Groups[1].Value, dark);
        }

        var themed = LightDark().Match(value);
        if (themed.Success)
        {
            return Resolve(themed.Groups[dark ? 2 : 1].Value, dark, token);
        }

        Assert.True(Hex().IsMatch(value), $"--{token} is not a plain color: {value}");
        return value.ToUpperInvariant();
    }

    private static double Luminance(string hex)
    {
        static double Channel(string hex, int offset)
        {
            var value = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(hex, 1)) + (0.7152 * Channel(hex, 3)) + (0.0722 * Channel(hex, 5));
    }

    private static DesignTokens Load()
    {
        var css = Comment().Replace(File.ReadAllText(FilePath), "");
        var start = css.IndexOf(":root {", StringComparison.Ordinal);
        var end = css.IndexOf('}', start);
        var block = css[start..end];

        return new DesignTokens(Declaration().Matches(block)
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value));
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"--([a-z0-9-]+)\s*:\s*([^;]+);")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^var\(--([a-z0-9-]+)\)$")]
    private static partial Regex Reference();

    [GeneratedRegex(@"^light-dark\(\s*([^,]+?)\s*,\s*([^,]+?)\s*\)$")]
    private static partial Regex LightDark();

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex Hex();
}
