using Microsoft.EntityFrameworkCore;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Identity;
using Autestiq.Domain.Projects;
using Autestiq.Persistence;

namespace Autestiq.Application.Identity;

/// <summary>The organization in context, read-only (E1.4, ADR-0043). Owners and admins only.</summary>
internal sealed class OrganizationService(
    AutestiqDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext)
    : IOrganizationService
{
    /// <inheritdoc />
    public async Task<OrganizationProfile?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageOrganization))
        {
            return null;
        }

        var organizationId = organizationContext.RequireOrganizationId();

        // The organizations table is the tenant boundary itself, so it has no query filter: the organization in
        // context is chosen by its id here, and only that one.
        var profile = await (
                from organization in database.Organizations
                where organization.Id == organizationId
                join version in database.PlanVersions on organization.PlanVersionId equals version.Id
                join plan in database.Plans on version.PlanId equals plan.Id
                select new
                {
                    organization.Name,
                    organization.Slug,
                    organization.DataRegion,
                    organization.RequiresTwoFactor,
                    organization.CreatedAt,
                    PlanName = plan.Name,
                    Version = version.Number,
                    version.EffectiveFrom,
                    version.MonthlyBrowserMinutes,
                    version.ConcurrentRuns,
                    version.MaxProjects,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return null;
        }

        // Projects and memberships are inside the boundary: the query filter keeps both to this organization.
        var projects = await database.Projects
            .CountAsync(project => project.Status == ProjectStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        var members = await database.Memberships
            .CountAsync(membership => membership.Status == MembershipStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return new OrganizationProfile(
            profile.Name,
            profile.Slug,
            profile.DataRegion,
            profile.RequiresTwoFactor,
            profile.CreatedAt,
            profile.PlanName,
            profile.Version,
            profile.EffectiveFrom,
            profile.MonthlyBrowserMinutes,
            profile.ConcurrentRuns,
            profile.MaxProjects,
            projects,
            members);
    }
}
