using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Testo.UI.Shared.Components;

/// <summary>
/// Base class for components that write the current URL into their markup, for example as
/// the return address of a form. It re-renders when the URL changes during interactive
/// navigation, where a component without parameters would otherwise keep the old URL.
/// </summary>
public abstract class CurrentUrlComponentBase : ComponentBase, IDisposable
{
    // Set by the Blazor runtime before OnInitialized runs, so it is never null in use.
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// The current URL relative to the site root, with query string and without fragment,
    /// for example <c>/app/settings?tab=team</c>.
    /// </summary>
    protected string CurrentUrl { get; private set; } = "/";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        CurrentUrl = ToRootRelative(Navigation.Uri);
        Navigation.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        CurrentUrl = ToRootRelative(e.Location);
        _ = InvokeAsync(StateHasChanged);
    }

    private string ToRootRelative(string absoluteUri)
    {
        var relative = Navigation.ToBaseRelativePath(absoluteUri);
        var fragment = relative.IndexOf('#', StringComparison.Ordinal);
        return "/" + (fragment < 0 ? relative : relative[..fragment]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops listening for URL changes.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Navigation.LocationChanged -= OnLocationChanged;
        }
    }
}
