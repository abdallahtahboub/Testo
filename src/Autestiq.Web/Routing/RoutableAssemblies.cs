using System.Reflection;
using Autestiq.Platform.Modules;
using Autestiq.UI.Shared.Showcase;
using Autestiq.Website.Pages;

namespace Autestiq.Web.Routing;

/// <summary>
/// The assemblies whose pages are routable in addition to the host's own: the public website
/// and every registered feature module. The host never lists individual pages.
/// </summary>
internal sealed class RoutableAssemblies(IEnumerable<IFeatureModule> modules)
{
    public IReadOnlyList<Assembly> Additional { get; } =
    [
        typeof(HomePage).Assembly,
        typeof(ShowcasePage).Assembly,
        .. modules.Select(module => module.Assembly).Distinct(),
    ];
}
