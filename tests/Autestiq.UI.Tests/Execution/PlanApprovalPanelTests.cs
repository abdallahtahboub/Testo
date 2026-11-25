using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Export;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Testing;
using Autestiq.Modules.Execution;
using Autestiq.Modules.Execution.Components;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Execution;

/// <summary>
/// The approval panel's download (Milestone 5.1 R4): an approved plan whose pass would rest on the test account's own
/// value is not offered for download, and the panel says why, by the value's name. The service refuses it too.
/// </summary>
public class PlanApprovalPanelTests : DesignSystemTestContext
{
    public PlanApprovalPanelTests()
    {
        Services.AddSingleton<ITestCaseService>(new NoTestCases());
        Services.AddSingleton<ICurrentUser>(new Viewer());
        new ExecutionModule().RegisterServices(Services);
    }

    private static TestCaseDetail Approved(IReadOnlyList<string> refusedFor) =>
        new(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            7,
            "Sign in and check the account page",
            "Staging",
            "https://staging.northwind.example",
            TestCaseStatus.Planned,
            null,
            [new PlannedAction(ActionKind.Goto, "Open the sign-in page", Path: "/sign-in")],
            PlanCoverage.Full,
            [],
            "claude-sonnet-5-5",
            0.05m,
            [],
            null,
            [],
            false,
            new PlanApproval(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero), "Mara Lindqvist", ToChangeData: false),
            [],
            [])
        {
            ExportRefusedFor = refusedFor,
        };

    [Fact]
    public void Render_APlanThatRestsOnTheAccountsOwnValue_OffersNoDownload_AndSaysWhyByName()
    {
        var panel = Render<PlanApprovalPanel>(parameters => parameters.Add(component => component.Detail, Approved(["username"])));

        Assert.Empty(panel.FindAll("[data-testid='download-test']"));
        var refused = panel.Find("[data-testid='download-refused']").TextContent;
        Assert.Contains("cannot be downloaded", refused, StringComparison.Ordinal);
        Assert.Contains("'username'", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_APlanThatRestsOnTheAccountsOwnValue_SaysWhyInGerman()
    {
        using var german = new CultureScope("de");

        var panel = Render<PlanApprovalPanel>(parameters => parameters.Add(component => component.Detail, Approved(["username"])));

        Assert.Contains("kann nicht heruntergeladen werden", panel.Find("[data-testid='download-refused']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_AnApprovedPlanWithNothingRefused_OffersTheDownload()
    {
        var panel = Render<PlanApprovalPanel>(parameters => parameters.Add(component => component.Detail, Approved([])));

        Assert.NotNull(panel.Find("[data-testid='download-test']"));
        Assert.Empty(panel.FindAll("[data-testid='download-refused']"));
    }

    private sealed class Viewer : ICurrentUser
    {
        public bool IsSignedIn => true;

        public Guid? UserId => Guid.Parse("77777777-7777-7777-7777-777777777777");

        public string? Email => "viewer@northwind.example";

        public string? DisplayName => "Viewer";

        public Role? Role => Contracts.Identity.Role.Viewer;

        public bool Can(Permission permission) => permission == Permission.Read;
    }

    /// <summary>The panel only renders here; nothing it would call is expected.</summary>
    private sealed class NoTestCases : ITestCaseService
    {
        public Task<IReadOnlyList<TestCaseSummary>> ListAsync(Guid projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TestCaseDetail?> GetAsync(Guid testCaseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult<Guid>> DescribeAsync(Guid projectId, Guid environmentId, string description, IReadOnlyList<ExpectedResult>? expected = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult> SetExpectedResultsAsync(Guid testCaseId, IReadOnlyList<ExpectedResult> expected, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult> ApproveAsync(Guid testCaseId, bool allowChangingData, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult> WithdrawApprovalAsync(Guid testCaseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult<Guid>> RunAsync(Guid testCaseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperationResult<ExportedTest>> DownloadAsync(Guid testCaseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
