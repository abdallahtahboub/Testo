using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Identity;

namespace Testo.Persistence.Configurations;

internal sealed class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> builder)
    {
        builder.HasKey(reset => reset.Id);

        builder.Property(reset => reset.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(reset => reset.RequestedFromIp).HasMaxLength(64);

        // The link carries the token alone, so this lookup must find exactly one row.
        builder.HasIndex(reset => reset.TokenHash).IsUnique();

        // Asking for a second link cancels the ones before it, which reads by user.
        builder.HasIndex(reset => new { reset.UserId, reset.UsedAt });

        builder.HasOne(reset => reset.User)
            .WithMany()
            .HasForeignKey(reset => reset.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
