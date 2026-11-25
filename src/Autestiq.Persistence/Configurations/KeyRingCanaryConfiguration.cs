using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Operations;

namespace Autestiq.Persistence.Configurations;

internal sealed class KeyRingCanaryConfiguration : IEntityTypeConfiguration<KeyRingCanary>
{
    public void Configure(EntityTypeBuilder<KeyRingCanary> builder)
    {
        builder.HasKey(canary => canary.Id);
        // One row (KeyRingCanary.OnlyId): the primary key keeps a second instance from writing another.
        builder.Property(canary => canary.Ciphertext).HasMaxLength(2000).IsRequired();
    }
}
