using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Modules.Execution.Resources;
using Autestiq.Platform;

namespace Autestiq.Modules.Execution.Pages;

/// <summary>The organization's projects, and a form to add one.</summary>
public sealed partial class ProjectListPage : ComponentBase
{
    [Inject]
    private IProjectService ProjectService { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private string? LoadError { get; set; }

    private bool Busy { get; set; }

    /// <summary>
    /// Whether the page answers clicks and typing. Prerendered, it does not: the circuit then replaces every
    /// component, and what was typed in the meantime is lost, so the form stays disabled until it would work.
    /// </summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private IReadOnlyList<ProjectSummary> Projects { get; set; } = [];

    private bool CanManage => CurrentUser.Can(Permission.ManageProjects);

    private string? Name { get; set; }

    private string? Key { get; set; }

    private string? Description { get; set; }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        Loading = true;
        LoadError = null;

        try
        {
            Projects = await ProjectService.ListAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            LoadError = Localizer["Projects_LoadFailed"];
        }
        finally
        {
            Loading = false;
        }
    }

    private async Task CreateAsync()
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        Problem = null;

        try
        {
            var created = await ProjectService.CreateAsync(new NewProject(Name ?? "", Key ?? "", Description), CancellationToken.None)
                .ConfigureAwait(false);

            if (!created.Succeeded)
            {
                // The use case answers in resource keys, never in English (CLAUDE.md, Application).
                Problem = Localizer[created.Errors[0]];
                return;
            }

            Navigation.NavigateTo(Routes.Environments(created.Value!));
        }
        finally
        {
            Busy = false;
        }
    }
}
