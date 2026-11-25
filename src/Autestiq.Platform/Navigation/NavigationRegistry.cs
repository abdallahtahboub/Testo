using Autestiq.Platform.Modules;

namespace Autestiq.Platform.Navigation;

/// <summary>
/// The navigation every module registered, collected once at start-up (Milestone 5). The shell draws it and
/// hides what a switched-off feature flag hides, so no nav entry is ever hard-coded in the shell.
/// </summary>
public sealed class NavigationRegistry : INavigationRegistry
{
    private readonly List<NavItem> _items = [];

    /// <inheritdoc />
    public IReadOnlyList<NavItem> Items => _items;

    /// <inheritdoc />
    public void Add(NavItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.Exists(existing => string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A navigation item with the id '{item.Id}' is already registered. Ids must be unique.");
        }

        _items.Add(item);
    }

    /// <summary>The registry filled by every module given, in the order they were registered.</summary>
    public static NavigationRegistry From(IEnumerable<IFeatureModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var registry = new NavigationRegistry();

        foreach (var module in modules)
        {
            module.RegisterNavigation(registry);
        }

        return registry;
    }
}
