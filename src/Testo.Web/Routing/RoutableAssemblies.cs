using System.Reflection;
using Testo.Platform.Modules;
using Testo.Website.Pages;

namespace Testo.Web.Routing;

/// <summary>
/// The assemblies whose pages are routable in addition to the host's own: the public website
/// and every registered feature module. The host never lists individual pages.
/// </summary>
internal sealed class RoutableAssemblies(IEnumerable<IFeatureModule> modules)
{
    public IReadOnlyList<Assembly> Additional { get; } =
    [
        typeof(HomePage).Assembly,
        .. modules.Select(module => module.Assembly).Distinct(),
    ];
}
