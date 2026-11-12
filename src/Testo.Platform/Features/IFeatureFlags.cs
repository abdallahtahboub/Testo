namespace Testo.Platform.Features;

/// <summary>
/// Answers whether a feature flag is switched on. Flag names follow
/// <c>Module.&lt;Name&gt;</c>, <c>Capability.&lt;Name&gt;</c> and <c>Feature.&lt;Name&gt;</c>.
/// </summary>
/// <remarks>
/// UI code depends on this small interface instead of a feature-flag library, so the source
/// of the flags (configuration today, the backend later) can change without touching it.
/// </remarks>
public interface IFeatureFlags
{
    /// <summary>
    /// Returns whether <paramref name="flag"/> is on. <see langword="null"/> means
    /// "not gated" and is always on.
    /// </summary>
    ValueTask<bool> IsEnabledAsync(string? flag, CancellationToken cancellationToken = default);
}
