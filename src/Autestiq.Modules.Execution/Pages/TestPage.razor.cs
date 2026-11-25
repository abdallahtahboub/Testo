using System.Globalization;
using Microsoft.AspNetCore.Components;
using Autestiq.Contracts.Export;
using Autestiq.Contracts.Planning;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.UI.Shared.Components;

namespace Autestiq.Modules.Execution.Pages;

/// <summary>One described test and its plan, kept up to date while it explores and plans.</summary>
public sealed partial class TestPage : ComponentBase
{
    /// <summary>The project's key.</summary>
    [Parameter]
    public string ProjectKey { get; set; } = "";

    /// <summary>The test.</summary>
    [Parameter]
    public Guid TestCaseId { get; set; }

    [Inject]
    private IProjectService ProjectService { get; set; } = null!;

    [Inject]
    private ITestCaseService TestCases { get; set; } = null!;

    [Inject]
    private ITestExporter Exporter { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Inject]
    private DataTurns Turns { get; set; } = null!;

    private ITimer? _refresh;

    private bool Loading { get; set; } = true;

    private ProjectSummary? Project { get; set; }

    private TestCaseDetail? Detail { get; set; }

    private IReadOnlyList<PlanNote> Checks { get; set; } = [];

    private IReadOnlyList<PlanNote> AiNotes { get; set; } = [];

    /// <summary>The plan as the Python test it would export to; made by fixed templates, never by a model (ADR-0023).</summary>
    private string? Python { get; set; }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        Project = await Turns.TakeAsync(() => ProjectService.FindAsync(ProjectKey, CancellationToken.None)).ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
        Loading = false;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // While it explores or plans, look again every two seconds; only once interactive, where an update can reach
        // the screen.
        if (firstRender && RendererInfo.IsInteractive)
        {
            _refresh = TimeProvider.CreateTimer(_ => _ = InvokeAsync(RefreshAsync), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
    }

    private async Task RefreshAsync()
    {
        // A change being saved reloads the test itself when it is done; this tick is not needed.
        if (Detail?.Status is TestCaseStatus.Exploring or TestCaseStatus.Planning && !Turns.Busy)
        {
            await LoadAsync().ConfigureAwait(false);
            StateHasChanged();
        }
    }

    private async Task ReloadAsync()
    {
        await LoadAsync().ConfigureAwait(false);

        // Back on the renderer's dispatcher: the await above may have finished on another thread.
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var detail = await Turns.TakeAsync(() => TestCases.GetAsync(TestCaseId, CancellationToken.None)).ConfigureAwait(false);

        // A test of another project is not this page's, even within the organization.
        Detail = Project is not null && detail is not null ? detail : null;
        Checks = Detail?.Notes.Where(note => note.Source == PlanNoteSource.CheckedByAutestiq).ToList() ?? [];
        AiNotes = Detail?.Notes.Where(note => note.Source == PlanNoteSource.Ai).ToList() ?? [];
        Python = Detail is { Steps.Count: > 0 }
            ? Exporter.Export(new TestPlan(Detail.Description, Detail.Steps, [], Detail.Coverage), Detail.Evidence).Code
            : null;
    }

    /// <summary>The words for a kind of step, spelled out so that the resource tests can find each key.</summary>
    private static string ActionKey(ActionKind kind) => kind switch
    {
        ActionKind.Goto => "Action_Goto",
        ActionKind.Click => "Action_Click",
        ActionKind.Type => "Action_Type",
        ActionKind.Press => "Action_Press",
        ActionKind.WaitFor => "Action_WaitFor",
        ActionKind.Select => "Action_Select",
        ActionKind.AssertText => "Action_AssertText",
        ActionKind.AssertVisible => "Action_AssertVisible",
        ActionKind.AssertNotVisible => "Action_AssertNotVisible",
        ActionKind.AssertUrl => "Action_AssertUrl",
        _ => "Action_Goto",
    };

    /// <summary>The words for where a check's expected value came from (ADR-0034 §3), spelled out for the resource tests.</summary>
    private static string EvidenceKey(CheckEvidence evidence) => evidence switch
    {
        CheckEvidence.Observed => "Evidence_Observed",
        CheckEvidence.StatedByTester => "Evidence_StatedByTester",
        _ => "Evidence_Assumed",
    };

    /// <summary>The badge's tone; the words above say the same, so nothing rests on the colour.</summary>
    private static Tone EvidenceTone(CheckEvidence evidence) => evidence switch
    {
        CheckEvidence.Observed => Tone.Neutral,
        CheckEvidence.StatedByTester => Tone.Info,
        _ => Tone.Warning,
    };

    /// <summary>What a step acts on, as the plan names it.</summary>
    private static string TargetText(PlannedAction step)
    {
        var target = step.Target is { } named
            ? string.Create(CultureInfo.InvariantCulture, $" · {named.Role ?? named.Kind.ToString()} “{named.Value}”{(named.Within is { Length: > 0 } within ? $" in “{within}”" : "")}")
            : step.Path is { Length: > 0 } path ? $" · {path}" : "";

        return step.Text is { Length: > 0 } text && step.SecretName is null ? $"{target} → “{text}”" : target;
    }

    /// <inheritdoc />
    public void Dispose() => _refresh?.Dispose();
}
