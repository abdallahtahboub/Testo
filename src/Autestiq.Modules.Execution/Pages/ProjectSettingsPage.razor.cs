using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Modules.Execution.Resources;
using Autestiq.UI.Shared.Components;

namespace Autestiq.Modules.Execution.Pages;

/// <summary>A project's settings: how long its runs' screenshots are kept.</summary>
public sealed partial class ProjectSettingsPage : ComponentBase
{
    /// <summary>The project's key, as it appears in the address.</summary>
    [Parameter]
    public string ProjectKey { get; set; } = "";

    [Inject]
    private IProjectService ProjectService { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private bool Busy { get; set; }

    private bool Saved { get; set; }

    /// <summary>Prerendered, the page answers nothing; its controls stay disabled until they would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private ProjectSummary? Project { get; set; }

    private int Days { get; set; } = EvidenceRetention.DefaultDays;

    private bool CanManage => CurrentUser.Can(Permission.ManageProjects);

    /// <summary>The choices the service accepts, each spelled out so that the resource tests can find its key.</summary>
    private IReadOnlyList<TRadioOption<int>> Options =>
    [
        .. EvidenceRetention.AllowedDays.Select(days => new TRadioOption<int>(days, days switch
        {
            90 => Localizer["Settings_Evidence_Days90"],
            30 => Localizer["Settings_Evidence_Days30"],
            7 => Localizer["Settings_Evidence_Days7"],
            _ => Localizer["Settings_Evidence_None"],
        })),
    ];

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        Project = await ProjectService.FindAsync(ProjectKey, CancellationToken.None).ConfigureAwait(false);
        Days = Project?.EvidenceRetentionDays ?? EvidenceRetention.DefaultDays;
        Loading = false;
    }

    private async Task SaveAsync()
    {
        if (Busy || Project is null)
        {
            return;
        }

        Busy = true;
        Problem = null;
        Saved = false;

        try
        {
            var result = await ProjectService.SetEvidenceRetentionAsync(Project.ProjectId, Days, CancellationToken.None).ConfigureAwait(false);

            // The use cases answer in resource keys, never in English (CLAUDE.md, Application).
            Problem = result.Succeeded ? null : Localizer[result.Errors[0]].Value;
            Saved = result.Succeeded;
        }
        finally
        {
            Busy = false;
        }
    }
}
