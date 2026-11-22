using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Testing;
using Autestiq.Modules.Execution.Resources;
using Autestiq.Modules.Execution.ViewModels;

namespace Autestiq.Modules.Execution.Components;

/// <summary>A test's expected results: shown as the tester's, and changed while the plan is not approved.</summary>
public sealed partial class ExpectedResultsPanel : ComponentBase
{
    /// <summary>The test.</summary>
    [Parameter, EditorRequired]
    public TestCaseDetail Detail { get; set; } = null!;

    /// <summary>Raised after a change, so the page reads the test again.</summary>
    [Parameter]
    public EventCallback Changed { get; set; }

    [Inject]
    private ITestCaseService TestCases { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    private bool Busy { get; set; }

    private bool Editing { get; set; }

    private string? Problem { get; set; }

    private List<ExpectedResultRow> Rows { get; set; } = [];

    /// <summary>Prerendered, the panel answers nothing; its controls stay disabled until they would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    /// <summary>The service decides; this only spares a person a button that would be refused.</summary>
    private bool MayEdit =>
        CurrentUser.Can(Permission.ManageTestCases)
        && Detail.Approval is null
        && Detail.Status != TestCaseStatus.Planning;

    private void Edit()
    {
        Rows = ExpectedResultRow.FromResults(Detail.ExpectedResults);

        if (Rows.Count == 0)
        {
            Rows.Add(new ExpectedResultRow());
        }

        Problem = null;
        Editing = true;
    }

    private void Cancel()
    {
        Editing = false;
        Problem = null;
    }

    private async Task SaveAsync()
    {
        if (Busy)
        {
            return;
        }

        Busy = true;

        try
        {
            var saved = await TestCases.SetExpectedResultsAsync(Detail.TestCaseId, ExpectedResultRow.ToResults(Rows), CancellationToken.None)
                .ConfigureAwait(false);

            // The use cases answer in resource keys, never in English.
            Problem = saved.Errors.Count > 0 ? Localizer[saved.Errors[0]].Value : null;
            Editing = !saved.Succeeded;
        }
        finally
        {
            Busy = false;
        }

        // On the renderer's dispatcher, where the page may render: the await above may have left it.
        await InvokeAsync(() => Changed.InvokeAsync()).ConfigureAwait(false);
    }
}
