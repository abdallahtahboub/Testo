namespace Autestiq.Contracts.Identity;

/// <summary>Reading the organization in context (E1.4, ADR-0043). Changing it is not on this interface yet.</summary>
public interface IOrganizationService
{
    /// <summary>The organization in context, or <c>null</c> for somebody without <see cref="Permission.ManageOrganization"/>.</summary>
    Task<OrganizationProfile?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>An organization as its owners and admins see it: who it is, and what its plan version allows.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Slug">Its address in the product, unique among organizations.</param>
/// <param name="DataRegion">Where its data is meant to live; chosen at creation and not yet routed by (data-architecture.md §10).</param>
/// <param name="RequiresTwoFactor">Whether its members must sign in with a second factor.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="PlanName">The plan it is on.</param>
/// <param name="PlanVersion">The version of the plan, which it keeps until an operator moves it (ADR-0028).</param>
/// <param name="PlanSince">When that version took effect.</param>
/// <param name="MonthlyBrowserMinutes">Billable browser minutes a month.</param>
/// <param name="ConcurrentRuns">Runs at once.</param>
/// <param name="MaxProjects">Projects allowed, or <c>null</c> for no limit.</param>
/// <param name="ActiveProjects">Projects in use.</param>
/// <param name="ActiveMembers">Members who may sign in.</param>
public sealed record OrganizationProfile(
    string Name,
    string Slug,
    string DataRegion,
    bool RequiresTwoFactor,
    DateTimeOffset CreatedAt,
    string PlanName,
    int PlanVersion,
    DateTimeOffset PlanSince,
    long MonthlyBrowserMinutes,
    int ConcurrentRuns,
    int? MaxProjects,
    int ActiveProjects,
    int ActiveMembers);
