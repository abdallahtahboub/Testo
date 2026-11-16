using Microsoft.EntityFrameworkCore;
using Autestiq.Persistence;

namespace Autestiq.Application.Usage;

/// <summary>What an organization's plan version allows (Milestone 3.7, ADR-0028).</summary>
/// <param name="PlanCode">The plan's code.</param>
/// <param name="PlanName">The plan's name as it is shown.</param>
/// <param name="Version">The version the organization is on.</param>
/// <param name="MonthlyBrowserMinutes">Billable browser minutes a month.</param>
/// <param name="ConcurrentRuns">Runs at once.</param>
/// <param name="MaxProjects">Projects, or <c>null</c> for no limit.</param>
public sealed record PlanEntitlements(
    string PlanCode,
    string PlanName,
    int Version,
    long MonthlyBrowserMinutes,
    int ConcurrentRuns,
    int? MaxProjects);

/// <summary>Reads what an organization may use, from the plan version it holds and nowhere else.</summary>
public static class Entitlements
{
    /// <summary>The organization's entitlements. Every organization holds a version: the database refuses one that does not.</summary>
    public static async Task<PlanEntitlements> ForAsync(AutestiqDbContext database, Guid organizationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var entitlements = await (
                from organization in database.Organizations
                where organization.Id == organizationId
                join version in database.PlanVersions on organization.PlanVersionId equals version.Id
                join plan in database.Plans on version.PlanId equals plan.Id
                select new PlanEntitlements(plan.Code, plan.Name, version.Number, version.MonthlyBrowserMinutes, version.ConcurrentRuns, version.MaxProjects))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // An organization that does not exist may use nothing.
        return entitlements ?? new PlanEntitlements("none", "None", 0, MonthlyBrowserMinutes: 0, ConcurrentRuns: 0, MaxProjects: 0);
    }

    /// <summary>
    /// Whether the organization may add one more project under its plan. Built for Milestone 5, which adds project
    /// creation; nothing creates projects outside development data before then.
    /// </summary>
    public static async Task<bool> MayAddProjectAsync(AutestiqDbContext database, Guid organizationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var allowed = (await ForAsync(database, organizationId, cancellationToken).ConfigureAwait(false)).MaxProjects;

        if (allowed is null)
        {
            return true;
        }

        // Across the filter, held to the organization in hand, so it answers the same with or without one in context.
        var projects = await database.Projects.IgnoreQueryFilters()
            .CountAsync(project => project.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        return projects < allowed;
    }
}
