using Microsoft.EntityFrameworkCore;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Usage;
using Autestiq.Persistence;

namespace Autestiq.Application.Usage;

/// <summary>An organization's usage this month, through the tenant filter.</summary>
internal sealed class UsageService(
    AutestiqDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
    : IUsageService
{
    /// <inheritdoc />
    public async Task<OrganizationUsage?> GetCurrentMonthAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read))
        {
            return null;
        }

        var organizationId = organizationContext.RequireOrganizationId();
        var period = UsageMeter.PeriodOf(timeProvider.GetUtcNow());

        var limit = await Entitlements.ForAsync(database, organizationId, cancellationToken).ConfigureAwait(false);

        var seconds = await database.UsageEvents
            .Where(usage => usage.Period == period && usage.Billable && usage.Category == UsageCategory.BrowserSeconds)
            .SumAsync(usage => usage.Quantity, cancellationToken)
            .ConfigureAwait(false);

        var runs = await database.UsageEvents
            .Where(usage => usage.Period == period && usage.Billable && usage.Category == UsageCategory.Run)
            .SumAsync(usage => usage.Quantity, cancellationToken)
            .ConfigureAwait(false);

        return new OrganizationUsage(period, UsageMeter.MinutesFor(seconds), limit.MonthlyBrowserMinutes, runs, limit.ConcurrentRuns, limit.PlanName, limit.Version);
    }
}
