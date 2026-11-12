namespace Testo.Platform.Navigation;

/// <summary>
/// Collects the navigation entries of all modules. The app shell renders whatever is
/// registered; it has no hard-coded entries of its own.
/// </summary>
public interface INavigationRegistry
{
    /// <summary>Adds an entry.</summary>
    void Add(NavItem item);

    /// <summary>All registered entries.</summary>
    IReadOnlyList<NavItem> Items { get; }
}
