using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Autestiq.Domain.Execution;

namespace Autestiq.Persistence.Configurations;

internal sealed class OriginProofConfiguration : IEntityTypeConfiguration<OriginProof>
{
    public void Configure(EntityTypeBuilder<OriginProof> builder)
    {
        builder.HasKey(proof => proof.Id);

        builder.Property(proof => proof.Origin).HasMaxLength(500).IsRequired();
        builder.Property(proof => proof.Token).HasMaxLength(100).IsRequired();
        builder.Property(proof => proof.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(proof => proof.FailureReason).HasMaxLength(2000);

        // One proof per organization and origin; another organization's is a row of its own.
        builder.HasIndex(proof => new { proof.OrganizationId, proof.Origin }).IsUnique();

        // The dispatcher asks for checks that wait.
        builder.HasIndex(proof => new { proof.Status, proof.CheckRequestedAt });

        // A check finishing and a new one being asked for can race; whoever saves second must know.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
