using Autestiq.Platform.Features;

namespace Autestiq.UI.Tests.Support;

/// <summary>Feature flags for tests: every flag on, except those named as off.</summary>
public sealed class SwitchedFlags(params string[] off) : IFeatureFlags
{
    /// <inheritdoc />
    public ValueTask<bool> IsEnabledAsync(string? flag, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(flag is null || !off.Contains(flag, StringComparer.Ordinal));
}
