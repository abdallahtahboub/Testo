using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Modules.Execution.Resources;

namespace Autestiq.Modules.Execution.Pages;

/// <summary>A project's environments and their ownership proofs.</summary>
public sealed partial class EnvironmentsPage : ComponentBase
{
    /// <summary>The project's key, as it appears in the address.</summary>
    [Parameter]
    public string ProjectKey { get; set; } = "";

    [Inject]
    private IProjectService ProjectService { get; set; } = null!;

    [Inject]
    private IOriginProofService Proofs { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private string? LoadError { get; set; }

    private bool Busy { get; set; }

    /// <summary>
    /// Whether the page answers clicks and typing. Prerendered, it does not: the circuit then replaces every
    /// component, and what was typed in the meantime is lost, so the form stays disabled until it would work.
    /// </summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private ProjectSummary? Project { get; set; }

    private IReadOnlyList<EnvironmentSummary> Environments { get; set; } = [];

    /// <summary>What to publish, per origin, once a person has asked for it on this page.</summary>
    private Dictionary<string, OriginProofRequest> Requests { get; } = new(StringComparer.Ordinal);

    private bool CanManage => CurrentUser.Can(Permission.ManageProjects);

    private string? Name { get; set; }

    private string? Origin { get; set; }

    /// <summary>The add form's choice. Off by default: production is never the default.</summary>
    private bool IsProduction { get; set; }

    /// <summary>The production environments whose acknowledgement an owner has ticked on this page.</summary>
    private HashSet<Guid> Acknowledged { get; } = [];

    private bool IsOwner => CurrentUser.Role == Role.Owner;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        Project = await ProjectService.FindAsync(ProjectKey, CancellationToken.None).ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
        Loading = false;
    }

    private async Task LoadAsync()
    {
        LoadError = null;

        if (Project is null)
        {
            return;
        }

        try
        {
            Environments = await ProjectService.ListEnvironmentsAsync(Project.ProjectId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            LoadError = Localizer["Environments_LoadFailed"];
        }
    }

    private async Task AddAsync()
    {
        if (Project is null)
        {
            return;
        }

        await DoAsync(async () =>
        {
            var kind = IsProduction ? EnvironmentKind.Production : EnvironmentKind.Test;
            var added = await ProjectService.AddEnvironmentAsync(Project.ProjectId, new NewEnvironment(Name ?? "", Origin ?? "", kind), CancellationToken.None)
                .ConfigureAwait(false);

            if (added.Succeeded)
            {
                // Emptied for the next one; ProjectTests checks the browser shows it empty.
                Name = "";
                Origin = "";
                IsProduction = false;
            }

            return added.Errors;
        }).ConfigureAwait(false);
    }

    private Task ProveAsync(string origin) => DoAsync(async () =>
    {
        var request = await Proofs.RequestAsync(origin, CancellationToken.None).ConfigureAwait(false);

        if (request.Succeeded)
        {
            Requests[origin] = request.Value!;
        }

        return request.Errors;
    });

    private void Acknowledge(Guid environmentId, bool acknowledged)
    {
        if (acknowledged)
        {
            Acknowledged.Add(environmentId);
        }
        else
        {
            Acknowledged.Remove(environmentId);
        }
    }

    // The service refuses without the acknowledgement; the page passes on exactly what the owner ticked.
    private Task AuthorizeAsync(Guid environmentId) => DoAsync(async () =>
        (await ProjectService.AuthorizeProductionAsync(environmentId, Acknowledged.Contains(environmentId), CancellationToken.None).ConfigureAwait(false)).Errors);

    private Task WithdrawAsync(Guid environmentId) => DoAsync(async () =>
    {
        Acknowledged.Remove(environmentId);
        return (await ProjectService.WithdrawProductionAsync(environmentId, CancellationToken.None).ConfigureAwait(false)).Errors;
    });

    private Task CheckAsync(string origin) => DoAsync(async () =>
        (await Proofs.CheckAsync(origin, CancellationToken.None).ConfigureAwait(false)).Errors);

    private Task RemoveAsync(Guid environmentId) => DoAsync(async () =>
        (await ProjectService.RemoveEnvironmentAsync(environmentId, CancellationToken.None).ConfigureAwait(false)).Errors);

    /// <summary>Runs one change, shows its first error in the person's language, and reloads the list.</summary>
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
