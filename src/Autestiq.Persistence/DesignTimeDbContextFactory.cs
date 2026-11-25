using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Autestiq.Contracts.Identity;

namespace Autestiq.Persistence;

/// <summary>
/// Builds a context for the <c>dotnet ef</c> tooling, which runs without the application and so
/// without a request, a signed-in person or an organization.
/// </summary>
/// <remarks>
/// Only the shape of the model matters when a migration is written, never the data, which is why
/// the organization here is deliberately empty. The connection string comes from
/// <c>AUTESTIQ_DB</c> so that generating a migration does not need the application's configuration.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AutestiqDbContext>
{
    /// <inheritdoc />
    public AutestiqDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTESTIQ_DB")
            ?? "Host=localhost;Database=autestiq_dev;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AutestiqDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AutestiqDbContext(options, new NoOrganization());
    }

    /// <summary>No request, so no organization.</summary>
    private sealed class NoOrganization : IOrganizationContext
    {
        public Guid? OrganizationId => null;

        public Guid RequireOrganizationId() =>
            throw new InvalidOperationException("The design-time context has no organization.");
    }
}
