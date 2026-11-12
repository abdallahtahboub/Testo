using Microsoft.FeatureManagement;
using Testo.Platform.Features;

namespace Testo.Web.Features;

/// <summary>
/// Answers feature-flag questions from the <c>FeatureManagement</c> configuration section,
/// using Microsoft.FeatureManagement.
/// </summary>
internal sealed class FeatureManagementFlags(IVariantFeatureManager featureManager) : IFeatureFlags
{
    public async ValueTask<bool> IsEnabledAsync(string? flag, CancellationToken cancellationToken = default) =>
        flag is null || await featureManager.IsEnabledAsync(flag, cancellationToken);
}
