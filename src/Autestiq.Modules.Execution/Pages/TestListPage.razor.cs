using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Contracts.Testing;
using Autestiq.Modules.Execution.Resources;
using Autestiq.Modules.Execution.ViewModels;
using Autestiq.Platform;

namespace Autestiq.Modules.Execution.Pages;

/// <summary>A project's described tests, the AI planning permission, and the form that describes a test.</summary>
public sealed partial class TestListPage : ComponentBase
{
    /// <summary>The project's key, as it appears in the address.</summary>
    [Parameter]
    public string ProjectKey { get; set; } = "";

    [Inject]
    private IProjectService ProjectService { get; set; } = null!;

    [Inject]
    private ITestCaseService TestCases { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private string? LoadError { get; set; }

    private bool Busy { get; set; }

    /// <summary>Prerendered, the page answers nothing; the form stays disabled until it would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private ProjectSummary? Project { get; set; }

    private IReadOnlyList<EnvironmentSummary> Environments { get; set; } = [];

    private IReadOnlyList<TestCaseSummary> Tests { get; set; } = [];

    private bool CanManageProjects => CurrentUser.Can(Permission.ManageProjects);

    private bool CanDescribe => CurrentUser.Can(Permission.ManageTestCases);

    private bool Acknowledged { get; set; }

    private string? EnvironmentId { get; set; }

    private string? Description { get; set; }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        await LoadAsync().ConfigureAwait(false);
        Loading = false;
    }

    private async Task LoadAsync()
    {
        LoadError = null;

        try
        {
            Project = await ProjectService.FindAsync(ProjectKey, CancellationToken.None).ConfigureAwait(false);

            if (Project is null)
            {
                return;
            }

            // Only environments a run may be aimed at, as far as their kind goes.
            Environments = [.. (await ProjectService.ListEnvironmentsAsync(Project.ProjectId, CancellationToken.None).ConfigureAwait(false)).Where(environment => environment.KindAllowsRuns)];
            EnvironmentId ??= Environments.Count > 0 ? Environments[0].EnvironmentId.ToString() : null;
            Tests = await TestCases.ListAsync(Project.ProjectId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            LoadError = Localizer["Tests_LoadFailed"];
        }
    }

    // The service refuses without the acknowledgement; the page passes on exactly what was ticked.
    private Task AllowAsync() => DoAsync(async () =>
        (await ProjectService.AllowAiProcessingAsync(Project!.ProjectId, Acknowledged, CancellationToken.None).ConfigureAwait(false)).Errors); // set: the button is shown only with a project.

    private Task WithdrawAsync() => DoAsync(async () =>
    {
        Acknowledged = false;
        return (await ProjectService.WithdrawAiProcessingAsync(Project!.ProjectId, CancellationToken.None).ConfigureAwait(false)).Errors; // as above.
    });

    /// <summary>What the tester says the application should show (ADR-0034), while the form is filled in.</summary>
    private List<ExpectedResultRow> Expected { get; } = [];

    private async Task DescribeAsync()
    {
        if (Project is null || !Guid.TryParse(EnvironmentId, out var environmentId))
        {
            Problem = Localizer["Tests_Error_NoSuchEnvironment"];
            return;
        }

        await DoAsync(async () =>
        {
            var described = await TestCases.DescribeAsync(
                Project.ProjectId,
                environmentId,
                Description ?? "",
                ExpectedResultRow.ToResults(Expected),
                CancellationToken.None).ConfigureAwait(false);

            if (described.Succeeded)
            {
                Navigation.NavigateTo(Routes.Test(Project.Key, described.Value));
            }

            return described.Errors;
        }).ConfigureAwait(false);
    }

    /// <summary>Runs one change, shows its first error in the person's language, and reloads.</summary>
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
            var errors = await change().ConfigureAwait(false);

            // The use cases answer in resource keys, never in English (CLAUDE.md, Application).
            Problem = errors.Count > 0 ? Localizer[errors[0]].Value : null;
            await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            Busy = false;
        }
    }
}
