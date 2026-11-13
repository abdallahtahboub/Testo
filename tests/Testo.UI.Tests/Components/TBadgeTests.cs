using Bunit;
using Testo.Contracts.Testing;
using Testo.UI.Shared.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Components;

public class TBadgeTests : DesignSystemTestContext
{
    [Fact]
    public void Render_Default_IsASubtleNeutralBadgeWithItsText()
    {
        var badge = Render<TBadge>(parameters => parameters.AddChildContent("12 open")).Find(".t-badge");

        Assert.Contains("t-badge--neutral", badge.ClassList);
        Assert.Contains("t-badge--subtle", badge.ClassList);
        Assert.Equal("12 open", badge.TextContent.Trim());
    }

    [Theory]
    [InlineData(Tone.Brand, "t-badge--brand")]
    [InlineData(Tone.Success, "t-badge--success")]
    [InlineData(Tone.Warning, "t-badge--warning")]
    [InlineData(Tone.Danger, "t-badge--danger")]
    [InlineData(Tone.Info, "t-badge--info")]
    public void Render_Tone_UsesItsClass(Tone tone, string expected)
    {
        var badge = Render<TBadge>(parameters => parameters
            .Add(component => component.Tone, tone)
            .AddChildContent("x")).Find(".t-badge");

        Assert.Contains(expected, badge.ClassList);
    }

    [Theory]
    [InlineData(BadgeAppearance.Subtle, "t-badge--subtle")]
    [InlineData(BadgeAppearance.Filled, "t-badge--filled")]
    [InlineData(BadgeAppearance.Outline, "t-badge--outline")]
    public void Render_Appearance_UsesItsClass(BadgeAppearance appearance, string expected)
    {
        var badge = Render<TBadge>(parameters => parameters
            .Add(component => component.Appearance, appearance)
            .AddChildContent("x")).Find(".t-badge");

        Assert.Contains(expected, badge.ClassList);
    }

    [Fact]
    public void Render_Icon_IsDecorativeBecauseTheWordCarriesTheMeaning()
    {
        var cut = Render<TBadge>(parameters => parameters
            .Add(component => component.Icon, IconName.CircleCheck)
            .AddChildContent("Passed"));

        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
        Assert.Contains("Passed", cut.Find(".t-badge").TextContent, StringComparison.Ordinal);
    }
}

public class TStatusBadgeTests : DesignSystemTestContext
{
    public static TheoryData<TestStatus, string, string> Statuses => new()
    {
        { TestStatus.Passed, "Passed", "Bestanden" },
        { TestStatus.Failed, "Failed", "Fehlgeschlagen" },
        { TestStatus.Blocked, "Blocked", "Blockiert" },
        { TestStatus.Skipped, "Skipped", "Übersprungen" },
        { TestStatus.NotRun, "Not run", "Nicht ausgeführt" },
        { TestStatus.Running, "Running", "Läuft" },
        { TestStatus.Flaky, "Flaky", "Instabil" },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Render_Status_ShowsItsWordInEachLanguage(TestStatus status, string english, string german)
    {
        Assert.Equal(english, Render<TStatusBadge>(parameters => parameters.Add(badge => badge.Status, status)).Find(".t-badge").TextContent.Trim());

        using var scope = new CultureScope("de");
        Assert.Equal(german, Render<TStatusBadge>(parameters => parameters.Add(badge => badge.Status, status)).Find(".t-badge").TextContent.Trim());
    }

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Render_Status_IsNeverShownByColourAlone(TestStatus status, string english, string german)
    {
        _ = (english, german);

        var cut = Render<TStatusBadge>(parameters => parameters.Add(badge => badge.Status, status));

        // Both an icon and a word, so the status survives greyscale and colour blindness.
        Assert.Single(cut.FindAll("svg"));
        Assert.NotEmpty(cut.Find(".t-badge__text").TextContent.Trim());
    }

    [Theory]
    [InlineData(TestStatus.Passed, Tone.Success)]
    [InlineData(TestStatus.Failed, Tone.Danger)]
    [InlineData(TestStatus.Running, Tone.Info)]
    [InlineData(TestStatus.Blocked, Tone.Warning)]
    [InlineData(TestStatus.Flaky, Tone.Warning)]
    [InlineData(TestStatus.Skipped, Tone.Neutral)]
    [InlineData(TestStatus.NotRun, Tone.Neutral)]
    public void ToneFor_Status_MatchesTheDesignSystem(TestStatus status, Tone expected)
    {
        Assert.Equal(expected, TestStatusVisuals.ToneFor(status));
    }

    [Fact]
    public void Render_EveryStatus_HasItsOwnIcon()
    {
        var icons = Enum.GetValues<TestStatus>().Select(TestStatusVisuals.IconFor).ToList();

        // Passed and failed must never share an icon; skipped and not-run reasonably can.
        Assert.NotEqual(TestStatusVisuals.IconFor(TestStatus.Passed), TestStatusVisuals.IconFor(TestStatus.Failed));
        Assert.True(icons.Distinct().Count() >= 6, "Statuses are sharing too many icons to tell apart.");
    }
}

public class TSeverityBadgeTests : DesignSystemTestContext
{
    [Theory]
    [InlineData(DefectSeverity.Critical, "Critical", "Kritisch")]
    [InlineData(DefectSeverity.Major, "Major", "Schwerwiegend")]
    [InlineData(DefectSeverity.Minor, "Minor", "Gering")]
    [InlineData(DefectSeverity.Trivial, "Trivial", "Kosmetisch")]
    public void Render_Severity_ShowsItsWordInEachLanguage(DefectSeverity severity, string english, string german)
    {
        Assert.Equal(english, Render<TSeverityBadge>(parameters => parameters.Add(badge => badge.Severity, severity)).Find(".t-badge").TextContent.Trim());

        using var scope = new CultureScope("de");
        Assert.Equal(german, Render<TSeverityBadge>(parameters => parameters.Add(badge => badge.Severity, severity)).Find(".t-badge").TextContent.Trim());
    }

    [Fact]
    public void Appearance_TheTwoThatNeedActing_AreDrawnStrongest()
    {
        Assert.Equal(BadgeAppearance.Filled, DefectSeverityVisuals.AppearanceFor(DefectSeverity.Critical));
        Assert.Equal(BadgeAppearance.Filled, DefectSeverityVisuals.AppearanceFor(DefectSeverity.Major));
        Assert.Equal(BadgeAppearance.Outline, DefectSeverityVisuals.AppearanceFor(DefectSeverity.Minor));
        Assert.Equal(BadgeAppearance.Outline, DefectSeverityVisuals.AppearanceFor(DefectSeverity.Trivial));
    }
}
