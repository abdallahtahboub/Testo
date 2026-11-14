using Bunit;
using Testo.Contracts.Execution;
using Testo.Modules.Execution.Components;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Execution;

/// <summary>
/// The badge is used on both execution pages, so it is tested: a status must read as itself,
/// in words, in both languages, and never by colour alone.
/// </summary>
public class RunStatusBadgeTests : DesignSystemTestContext
{
    [Theory]
    [InlineData(RunStatus.Queued, "Queued")]
    [InlineData(RunStatus.Running, "Running")]
    [InlineData(RunStatus.Passed, "Passed")]
    [InlineData(RunStatus.Failed, "Failed")]
    [InlineData(RunStatus.Cancelled, "Stopped")]
    [InlineData(RunStatus.TimedOut, "Out of time")]
    [InlineData(RunStatus.BudgetExceeded, "Over budget")]
    [InlineData(RunStatus.InfrastructureError, "Could not run")]
    [InlineData(RunStatus.Inconclusive, "Inconclusive")]
    public void Render_EveryStatus_SaysItInWords(RunStatus status, string expected)
    {
        var badge = Render<RunStatusBadge>(parameters => parameters
            .Add(component => component.Status, status)).Find(".t-badge");

        Assert.Equal(expected, badge.TextContent.Trim());
    }

    [Fact]
    public void Render_AnEndingThatIsNotTheApplicationsFault_IsNotShownAsAFailure()
    {
        // The point of the distinction: out of time, over budget and could-not-run are warnings,
        // because calling them failures would be a lie about the application under test.
        RunStatus[] endings =
        [
            RunStatus.TimedOut, RunStatus.BudgetExceeded, RunStatus.InfrastructureError,

            // Every step of an inconclusive run passed, which is exactly why it must not be shown
            // in green: that is how a result answering half a question reads as answering all of it.
            RunStatus.Inconclusive,
        ];

        foreach (var status in endings)
        {
            var badge = Render<RunStatusBadge>(parameters => parameters
                .Add(component => component.Status, status)).Find(".t-badge");

            Assert.Contains("t-badge--warning", badge.ClassList);
            Assert.DoesNotContain("t-badge--danger", badge.ClassList);
        }
    }

    [Fact]
    public void Render_Failed_IsDangerAndCarriesAnIconAsWellAsTheWord()
    {
        var badge = Render<RunStatusBadge>(parameters => parameters
            .Add(component => component.Status, RunStatus.Failed)).Find(".t-badge");

        Assert.Contains("t-badge--danger", badge.ClassList);
        Assert.NotEmpty(badge.QuerySelectorAll("svg"));
        Assert.Equal("Failed", badge.TextContent.Trim());
    }

    [Fact]
    public void Render_InGerman_SaysItInGerman()
    {
        using var german = new CultureScope("de");

        var badge = Render<RunStatusBadge>(parameters => parameters
            .Add(component => component.Status, RunStatus.TimedOut)).Find(".t-badge");

        Assert.Equal("Zeit abgelaufen", badge.TextContent.Trim());
    }
}
