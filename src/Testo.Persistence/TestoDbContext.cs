using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Testo.Contracts.Identity;
using Testo.Domain.Audit;
using Testo.Domain.Common;
using Testo.Domain.Identity;
using Testo.Domain.Projects;

namespace Testo.Persistence;

/// <summary>
/// The database. One database shared by every organization, with the tenant boundary enforced
/// here rather than remembered in each query (ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// Every entity that implements <see cref="IBelongsToOrganization"/> gets a query filter adding
/// <c>WHERE organization_id = @current</c> automatically. Forgetting a <c>WHERE</c> clause is the
/// ordinary way tenant data leaks; here the clause is not written by hand, so it cannot be
/// forgotten.
/// </para>
/// <para>
/// The few places that genuinely need to look across the boundary — finding the invitation a link
/// refers to, before the person is a member of anything — say so with
/// <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}(IQueryable{TEntity})"/>.
/// That makes each one visible in review instead of silent.
/// </para>
/// </remarks>
public class TestoDbContext(DbContextOptions<TestoDbContext> options, IOrganizationContext organizationContext)
    : DbContext(options)
{
    /// <summary>
    /// The organization every filtered query is scoped to. <see cref="Guid.Empty"/> when there is
    /// none, which matches no row, so an unscoped query returns nothing rather than everything.
    /// </summary>
    /// <remarks>
    /// This is read by the query filters as a member of the context, which Entity Framework turns
    /// into a query parameter evaluated per query. It must stay a property for that reason: baking
    /// the value into the model would let the first request's organization be reused by the next.
    /// </remarks>
    public Guid CurrentOrganizationId => organizationContext.OrganizationId ?? Guid.Empty;

    /// <summary>The customer companies.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>The people, who sit outside the tenant boundary.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Who belongs where, and in what role.</summary>
    public DbSet<Membership> Memberships => Set<Membership>();

    /// <summary>Offers of a place that have not yet been taken up.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Pending "I have forgotten my password" links.</summary>
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

    /// <summary>What happened, append-only.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>The products under test.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestoDbContext).Assembly);

        ApplyOrganizationFilters(modelBuilder);
        SnakeCaseNames.ApplyTo(modelBuilder);
    }

    /// <summary>
    /// Adds the tenant filter to every entity inside the boundary, found by the interface rather
    /// than by a list, so a new entity is covered the day it is added.
    /// </summary>
    private void ApplyOrganizationFilters(ModelBuilder modelBuilder)
    {
        var applyFilter = typeof(TestoDbContext)
            .GetMethod(nameof(ApplyOrganizationFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.IsAssignableTo(typeof(IBelongsToOrganization)))
            {
                applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    /// <summary>
    /// Written as a normal lambda on purpose. The compiler closes over this context, so Entity
    /// Framework reads <see cref="CurrentOrganizationId"/> afresh for every query instead of
    /// capturing one organization into the cached model.
    /// </summary>
    private void ApplyOrganizationFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IBelongsToOrganization =>
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => entity.OrganizationId == CurrentOrganizationId);
}
