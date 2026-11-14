using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Testo.Contracts.Common;
using Testo.Contracts.Execution;
using Testo.Modules.Execution.Resources;
using Testo.Platform;

namespace Testo.Modules.Execution.Pages;

/// <summary>Every run of the current project, and the scenarios a person may start.</summary>
public sealed partial class RunListPage : ComponentBase
{
    private const int PageSize = 25;

    [Inject]
    private IExecutionService Execution { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>The data is on its way.</summary>
    private bool Loading { get; set; } = true;

    /// <summary>A start, cancel or rerun is in flight; a second click would queue a second run.</summary>
    private bool Busy { get; set; }

    /// <summary>What went wrong with the last thing the person asked for, in their language.</summary>
    private string? Problem { get; set; }

    private Guid? ProjectId { get; set; }

    private IReadOnlyList<ScenarioSummary> Scenarios { get; set; } = [];

    private PagedResult<TestRunSummary> Runs { get; set; } = PagedResult.Empty<TestRunSummary>();

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Scenarios = await Execution.ListScenariosAsync(CancellationToken.None).ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        Loading = true;

        ProjectId = await Execution.GetCurrentProjectAsync(CancellationToken.None).ConfigureAwait(false);

        Runs = ProjectId is { } projectId
            ? await Execution.ListRunsAsync(projectId, page: 1, PageSize, CancellationToken.None).ConfigureAwait(false)
            : PagedResult.Empty<TestRunSummary>();

        Loading = false;
    }

    private async Task StartAsync(string scenarioId)
    {
        if (Busy)
        {
            return;
        }

        if (ProjectId is not { } projectId)
        {
            Problem = Localizer["Execution_Error_NoSuchProject"];
            return;
        }

        Busy = true;
        Problem = null;

        try
        {
            var started = await Execution.StartRunAsync(projectId, scenarioId, CancellationToken.None)
                .ConfigureAwait(false);

            if (!started.Succeeded)
            {
                // The use case answers in resource keys, never in English (CLAUDE.md, Application).
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
}
