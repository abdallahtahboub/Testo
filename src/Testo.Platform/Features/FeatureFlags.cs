namespace Testo.Platform.Features;

/// <summary>
/// The flags that are not a module or a capability. Named here rather than written as
/// strings where they are used, so a typo is a compile error and every flag is findable.
/// </summary>
public static class FeatureFlags
{
    /// <summary>The component showcase at <c>/showcase</c>. A development tool; off in production.</summary>
    public const string Showcase = "Feature.Showcase";
}
