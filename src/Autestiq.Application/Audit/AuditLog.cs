using Microsoft.EntityFrameworkCore;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Identity;
using Autestiq.Persistence;

namespace Autestiq.Application.Audit;

/// <summary>Reading the log. Owners and admins only; the query filter keeps it to their organization.</summary>
internal sealed class AuditLog(AutestiqDbContext database, ICurrentUser currentUser) : IAuditLog
{
    /// <inheritdoc />
    public async Task<PagedResult<AuditRecord>> ReadAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!currentUser.Can(Permission.ReadAuditLog))
        {
            return PagedResult.Empty<AuditRecord>();
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, AuditQuery.MaxPageSize);
        var entries = database.AuditEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            var actor = query.Actor.Trim();
            entries = entries.Where(entry => entry.ActorEmail == actor);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            entries = entries.Where(entry => entry.Action == action);
        }

        if (query.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(entry => entry.At >= start);
        }

        if (query.To is { } to)
        {
            // The whole of the last day: everything before the start of the next.
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(entry => entry.At < end);
        }

        var total = await entries.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await entries
            .OrderByDescending(entry => entry.At)
            .ThenByDescending(entry => entry.Id)
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

        return new PagedResult<AuditRecord>(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<AuditFilterChoices> ReadFilterChoicesAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ReadAuditLog))
        {
            return AuditFilterChoices.None;
        }

        var actors = await database.AuditEntries
            .Where(entry => entry.ActorEmail != null)
            // Not null: the line above keeps only entries with an address.
            .Select(entry => entry.ActorEmail!)
            .Distinct()
            .OrderBy(actor => actor)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var actions = await database.AuditEntries
            .Select(entry => entry.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AuditFilterChoices(actors, actions);
    }
}
