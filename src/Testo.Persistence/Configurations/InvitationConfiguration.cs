using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Identity;

namespace Testo.Persistence.Configurations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Email).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(32).IsRequired();

        // The link carries a token and nothing else, so this lookup happens across organizations
        // and must find exactly one row.
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();

        // The pending list of one organization.
        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.NormalizedEmail });

        builder.HasOne(invitation => invitation.Organization)
            .WithMany()
            .HasForeignKey(invitation => invitation.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
