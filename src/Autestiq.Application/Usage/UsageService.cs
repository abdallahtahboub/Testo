using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Usage;
using Autestiq.Persistence;

namespace Autestiq.Application.Usage;

/// <summary>An organization's usage this month, through the tenant filter.</summary>
internal sealed class UsageService(
    AutestiqDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    IOptionsMonitor<UsageOptions> options,
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

        var plan = await database.Organizations
            .Where(organization => organization.Id == organizationId)
            .Select(organization => organization.Plan)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var limit = options.CurrentValue.For(plan);

        var seconds = await database.UsageEvents
            .Where(usage => usage.Period == period && usage.Billable && usage.Category == UsageCategory.BrowserSeconds)
            .SumAsync(usage => usage.Quantity, cancellationToken)
            .ConfigureAwait(false);

        var runs = await database.UsageEvents
            .Where(usage => usage.Period == period && usage.Billable && usage.Category == UsageCategory.Run)
            .SumAsync(usage => usage.Quantity, cancellationToken)
            .ConfigureAwait(false);

        return new OrganizationUsage(period, UsageMeter.MinutesFor(seconds), limit.MonthlyBrowserMinutes, runs, limit.ConcurrentRuns);
    }
}
