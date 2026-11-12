using Testo.UI.Shared.Theming;

namespace Testo.UI.Tests.Design;

/// <summary>
/// Checks the color tokens against WCAG 2.2 AA in the light and in the dark theme:
/// 4.5 : 1 for text, 3 : 1 for the boundaries of controls and for the focus ring.
/// </summary>
public class TokenContrastTests
{
    private const double Text = 4.5;
    private const double NonText = 3.0;

    private static readonly string[] Surfaces = ["color-bg", "color-surface", "color-surface-subtle"];

    private static readonly string[] TextColors =
    [
        "color-text", "color-text-muted", "color-brand", "color-brand-hover", "color-link", "color-accent",
        "color-success", "color-warning", "color-danger", "color-info",
        "status-passed", "status-failed", "status-running", "status-blocked", "status-flaky", "status-skipped",
    ];

    /// <summary>Foreground token, background token, minimum ratio.</summary>
    public static TheoryData<string, string, double> Pairs
    {
        get
        {
            var pairs = new TheoryData<string, string, double>();

            foreach (var surface in Surfaces)
            {
                foreach (var color in TextColors)
                {
                    pairs.Add(color, surface, Text);
                }

                pairs.Add("color-border-strong", surface, NonText);
                pairs.Add("color-focus", surface, NonText);
            }

            // Text on tinted and filled backgrounds.
            pairs.Add("color-text", "color-brand-subtle", Text);
            pairs.Add("color-text-muted", "color-brand-subtle", Text);
            pairs.Add("color-brand", "color-brand-subtle", Text);
            pairs.Add("color-on-brand", "color-brand", Text);
            pairs.Add("color-on-brand", "color-brand-hover", Text);
            pairs.Add("color-on-accent", "color-accent", Text);
            pairs.Add("color-on-hero", "color-hero-start", Text);
            pairs.Add("color-on-hero", "color-hero-end", Text);
            pairs.Add("color-on-hero-muted", "color-hero-start", Text);
            pairs.Add("color-on-hero-muted", "color-hero-end", Text);

            return pairs;
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void LightTheme_Pair_MeetsItsMinimumContrast(string foreground, string background, double minimum)
    {
        AssertContrast(foreground, background, minimum, dark: false);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void DarkTheme_Pair_MeetsItsMinimumContrast(string foreground, string background, double minimum)
    {
        AssertContrast(foreground, background, minimum, dark: true);
    }

    [Fact]
    public void FluentBrandColor_Always_EqualsTheBrandToken()
    {
        Assert.Equal(FluentTheme.BrandColor, DesignTokens.Current.Color("color-brand", dark: false), ignoreCase: true);
    }

    private static void AssertContrast(string foreground, string background, double minimum, bool dark)
    {
        var tokens = DesignTokens.Current;
        var ratio = tokens.Contrast(foreground, background, dark);

        Assert.True(
            ratio >= minimum,
            $"--{foreground} ({tokens.Color(foreground, dark)}) on --{background} ({tokens.Color(background, dark)}) " +
            $"has a contrast of {ratio:0.00} : 1 in the {(dark ? "dark" : "light")} theme; at least {minimum:0.0} : 1 is required.");
    }
}
