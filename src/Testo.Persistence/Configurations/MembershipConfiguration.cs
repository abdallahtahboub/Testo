using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Identity;

namespace Testo.Persistence.Configurations;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(membership => membership.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // A person holds one role per organization, never two.
        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId }).IsUnique();

        // The member list, which is read by organization and then by person.
        builder.HasIndex(membership => new { membership.OrganizationId, membership.Status });

        // Sign-in asks the other way round: which organizations does this person belong to.
        builder.HasIndex(membership => new { membership.UserId, membership.Status });

        builder.HasOne(membership => membership.Organization)
            .WithMany(organization => organization.Memberships)
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(membership => membership.User)
            .WithMany(user => user.Memberships)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
