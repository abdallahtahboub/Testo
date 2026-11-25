using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Testing;
using Autestiq.Modules.Execution.Resources;
using Autestiq.Platform;

namespace Autestiq.Modules.Execution.Components;

/// <summary>Approving a test's plan, withdrawing the approval, running it, and its runs.</summary>
public sealed partial class PlanApprovalPanel : ComponentBase
{
    /// <summary>The planned test.</summary>
    [Parameter, EditorRequired]
    public TestCaseDetail Detail { get; set; } = null!;

    /// <summary>Raised after the approval changed, so the page reads the test again.</summary>
    [Parameter]
    public EventCallback Changed { get; set; }

    [Inject]
    private ITestCaseService TestCases { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private DataTurns Turns { get; set; } = null!;

    private bool Busy { get; set; }

    /// <summary>Prerendered, the panel answers nothing; its controls stay disabled until they would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private bool AllowChange { get; set; }

    private bool CanApprove => CurrentUser.Can(Permission.ApproveTestArtifacts);

    private bool CanRun => CurrentUser.Can(Permission.RunTests);

    private Task ApproveAsync() => DoAsync(async () => (await TestCases.ApproveAsync(Detail.TestCaseId, AllowChange, CancellationToken.None).ConfigureAwait(false)).Errors);

    private Task WithdrawAsync() => DoAsync(async () => (await TestCases.WithdrawApprovalAsync(Detail.TestCaseId, CancellationToken.None).ConfigureAwait(false)).Errors);

    private async Task RunAsync()
    {
        Guid? started = null;

        await DoAsync(async () =>
        {
            var result = await TestCases.RunAsync(Detail.TestCaseId, CancellationToken.None).ConfigureAwait(false);
            started = result.Succeeded ? result.Value : null;
            return result.Errors;
        }).ConfigureAwait(false);

        // The run's own page shows it step by step as it happens.
        if (started is { } runId)
        {
            await InvokeAsync(() => Navigation.NavigateTo(Routes.Run(runId))).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one change, shows its first error in the person's language, and tells the page.</summary>
    private async Task DoAsync(Func<Task<IReadOnlyList<string>>> change)
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        Problem = null;

        try
        {
            var errors = await Turns.TakeAsync(change).ConfigureAwait(false);

            // The use cases answer in resource keys, never in English (CLAUDE.md, Application).
            Problem = errors.Count > 0 ? Localizer[errors[0]].Value : null;

            if (errors.Count == 0)
            {
                AllowChange = false;

                // On the renderer's dispatcher, where the page may render: the awaits above may have left it.
                await InvokeAsync(() => Changed.InvokeAsync()).ConfigureAwait(false);
            }
        }
        finally
        {
            Busy = false;
        }
    }
}
