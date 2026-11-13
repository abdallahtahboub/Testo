using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Testo.Domain.Audit;

namespace Testo.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Action).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.SubjectType).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.SubjectName).HasMaxLength(400);
        builder.Property(entry => entry.ActorEmail).HasMaxLength(320);
        builder.Property(entry => entry.IpAddress).HasMaxLength(64);

        // The before and after values differ in shape from one action to the next, which is what
        // jsonb is for: one column, queryable, without a table per kind of change (ADR-0011).
        builder.Property(entry => entry.Before).HasColumnType("jsonb");
        builder.Property(entry => entry.After).HasColumnType("jsonb");

        // The log is read newest first, within one organization.
        builder.HasIndex(entry => new { entry.OrganizationId, entry.At })
            .IsDescending(false, true);

        // "What happened to this object?" and "what did this person do?" are the other two
        // questions a security review asks.
        builder.HasIndex(entry => new { entry.OrganizationId, entry.SubjectType, entry.SubjectId });
        builder.HasIndex(entry => new { entry.OrganizationId, entry.ActorUserId, entry.At });

        // No concurrency token: an entry is written once and never updated, which the interceptor
        // enforces rather than trusts.
    }
}
