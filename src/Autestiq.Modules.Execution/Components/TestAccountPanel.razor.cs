using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Projects;
using Autestiq.Modules.Execution.Resources;

namespace Autestiq.Modules.Execution.Components;

/// <summary>A test environment's test account: its saved values by name, and a form to set them.</summary>
public sealed partial class TestAccountPanel : ComponentBase
{
    /// <summary>The environment, a test site.</summary>
    [Parameter, EditorRequired]
    public EnvironmentSummary Environment { get; set; } = null!;

    /// <summary>Whether the person may set and remove values (<c>ManageProjects</c>).</summary>
    [Parameter]
    public bool CanManage { get; set; }

    /// <summary>Raised after a change, so the page reads the environment again.</summary>
    [Parameter]
    public EventCallback Changed { get; set; }

    [Inject]
    private IProjectService ProjectService { get; set; } = null!;

    [Inject]
    private IStringLocalizer<ExecutionResources> Localizer { get; set; } = null!;

    private bool Busy { get; set; }

    /// <summary>Prerendered, the panel answers nothing; its controls stay disabled until they would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private string? Username { get; set; }

    private string? Password { get; set; }

    private async Task SaveAsync()
    {
        var errors = new List<string>();

        // Only what was typed; an empty field leaves its value as it is.
        foreach (var (name, value) in new[] { ("username", Username), ("password", Password) })
        {
            if (!string.IsNullOrEmpty(value))
            {
                errors.AddRange((await ProjectService.SetSecretAsync(Environment.EnvironmentId, name, value, CancellationToken.None).ConfigureAwait(false)).Errors);
            }
        }

        await FinishAsync(errors).ConfigureAwait(false);
    }

    private async Task DeleteAsync(string name) =>
        await FinishAsync((await ProjectService.DeleteSecretAsync(Environment.EnvironmentId, name, CancellationToken.None).ConfigureAwait(false)).Errors).ConfigureAwait(false);

    private async Task FinishAsync(IReadOnlyList<string> errors)
    {
        // The fields are emptied whatever happened: a value never stays on the page longer than it must.
        Username = null;
        Password = null;
        Problem = errors.Count > 0 ? Localizer[errors[0]].Value : null;

        // On the renderer's dispatcher, where the page may render: the awaits above may have left it.
        await InvokeAsync(() => Changed.InvokeAsync()).ConfigureAwait(false);
    }
}
