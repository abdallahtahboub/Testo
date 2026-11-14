using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Testo.Contracts.Execution;
using Testo.Modules.Execution.Resources;
using Testo.Platform;
using Testo.UI.Shared.Components;

namespace Testo.Modules.Execution.Pages;

/// <summary>One run: how it ended, its steps and the evidence each step left behind.</summary>
public sealed partial class RunPage : ComponentBase, IDisposable
{
    /// <summary>
    /// How often the page asks again while the run is still going. Short enough that a person
    /// watching sees it progress, long enough that it is not a load generator.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private ITimer? _poll;
    private bool _loading;

    /// <summary>Which run to show.</summary>
    [Parameter]
    public Guid RunId { get; set; }

    [Inject]
    private IExecutionService Execution { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private TimeProvider Clock { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private bool Busy { get; set; }

    private string? Problem { get; set; }

    private TestRunDetail? Run { get; set; }

    /// <summary>The run's number, for the browser tab, before the run itself has arrived.</summary>
    private string Number => Run is { } run ? run.Summary.Number.ToString(System.Globalization.CultureInfo.CurrentCulture) : "…";

    /// <inheritdoc />
    protected override Task OnParametersSetAsync() => LoadAsync();

    /// <summary>Fetches the run, and keeps the refresh timer in step with whether it is still going.</summary>
    private async Task LoadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;

        try
        {
            Run = await Execution.GetRunAsync(RunId, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _loading = false;
            Loading = false;
        }

        if (Run is { } run && run.Summary.Status.IsActive())
        {
            // One timer, restarted rather than replaced, so a long run does not accumulate them.
            _poll ??= Clock.CreateTimer(
                _ => _ = InvokeAsync(RefreshAsync),
                state: null,
                PollInterval,
                PollInterval);
        }
        else
        {
            Stop();
        }
    }

    private async Task RefreshAsync()
    {
        await LoadAsync().ConfigureAwait(false);
        StateHasChanged();
    }

    private async Task CancelAsync()
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        Problem = null;

        try
        {
            var cancelled = await Execution.CancelRunAsync(RunId, CancellationToken.None).ConfigureAwait(false);

            if (!cancelled.Succeeded)
            {
                Problem = Localizer[cancelled.Errors[0]];
                return;
            }

            await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task RerunAsync()
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        Problem = null;

        try
        {
            var started = await Execution.RerunAsync(RunId, CancellationToken.None).ConfigureAwait(false);

            if (!started.Succeeded)
            {
                Problem = Localizer[started.Errors[0]];
                return;
            }

            Navigation.NavigateTo(Routes.Run(started.Value));
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// A failure reason is either a resource key from the application or a sentence from the worker.
    /// A key that no resource file knows is shown as it came, rather than hidden.
    /// </summary>
    private string Describe(string reason)
    {
        var text = Localizer[reason];
        return text.ResourceNotFound ? reason : text.Value;
    }

    private static string Moment(DateTimeOffset? moment) =>
        moment is { } value ? value.ToLocalTime().ToString("G", System.Globalization.CultureInfo.CurrentCulture) : "—";

    private string Took(TestRunStepDetail step) =>
        step is { StartedAt: { } from, FinishedAt: { } to }
            ? Localizer["Run_Steps_Took", (to - from).TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)]
            : "";

    /// <summary>
    /// The text for a step's status. Written out rather than built from the enum's name, so that
    /// every key is one the resource tests can find.
    /// </summary>
    private static string StepKey(StepStatus status) => status switch
    {
        StepStatus.Pending => "StepStatus_Pending",
        StepStatus.Running => "StepStatus_Running",
        StepStatus.Passed => "StepStatus_Passed",
        StepStatus.Failed => "StepStatus_Failed",
        StepStatus.Skipped => "StepStatus_Skipped",
        _ => "StepStatus_Pending",
    };

    private static Tone StepTone(StepStatus status) => status switch
    {
        StepStatus.Passed => Tone.Success,
        StepStatus.Failed => Tone.Danger,
        StepStatus.Running => Tone.Info,
        _ => Tone.Neutral,
    };

    private void Stop()
    {
        _poll?.Dispose();
        _poll = null;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
