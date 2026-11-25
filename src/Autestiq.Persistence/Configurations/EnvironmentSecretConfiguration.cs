using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Projects;

namespace Autestiq.Persistence.Configurations;

internal sealed class EnvironmentSecretConfiguration : IEntityTypeConfiguration<EnvironmentSecret>
{
    public void Configure(EntityTypeBuilder<EnvironmentSecret> builder)
    {
        builder.HasKey(secret => secret.Id);

        builder.Property(secret => secret.Name).HasMaxLength(32).IsRequired();
        builder.Property(secret => secret.Ciphertext).HasMaxLength(4000).IsRequired();

        // One value per name per environment.
        builder.HasIndex(secret => new { secret.OrganizationId, secret.EnvironmentId, secret.Name }).IsUnique();

        // Removing an environment removes its secrets with it (ADR-0033 §7).
        builder.HasOne<TestEnvironment>()
            .WithMany()
            .HasForeignKey(secret => secret.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
