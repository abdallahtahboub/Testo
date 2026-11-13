using Microsoft.EntityFrameworkCore;
using Testo.Contracts.Common;
using Testo.Contracts.Identity;
using Testo.Persistence;

namespace Testo.Application.Audit;

/// <summary>Reading the log. Owners and admins only.</summary>
internal sealed class AuditLog(TestoDbContext database, ICurrentUser currentUser) : IAuditLog
{
    /// <inheritdoc />
    public async Task<PagedResult<AuditRecord>> ReadAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ReadAuditLog))
        {
            return PagedResult.Empty<AuditRecord>();
        }

        var query = database.AuditEntries.OrderByDescending(entry => entry.At);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new AuditRecord(
                entry.Id,
                entry.At,
                entry.ActorEmail,
                entry.Action,
                entry.SubjectType,
                entry.SubjectName,
                entry.IpAddress,
                entry.Before,
                entry.After))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditRecord>(entries, total, page, pageSize);
    }
}
