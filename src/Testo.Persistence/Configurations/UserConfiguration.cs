using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Identity;

namespace Testo.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(400);
        builder.Property(user => user.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(user => user.ConcurrencyStamp).HasMaxLength(64).IsRequired();
        builder.Property(user => user.AuthenticatorKey).HasMaxLength(200);
        builder.Property(user => user.Locale).HasMaxLength(10).IsRequired();
        builder.Property(user => user.Theme).HasMaxLength(10).IsRequired();

        // One account per address, whatever case it was typed in. This is the index sign-in reads,
        // and it is unique so that two accounts for one person cannot exist.
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
