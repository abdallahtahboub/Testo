using Autestiq.Contracts.Common;

namespace Autestiq.Contracts.Identity;

/// <summary>
/// Reading what happened. Writing is not on this interface on purpose: entries are written by the
/// use cases that cause them, so an entry cannot be forgotten or forged from outside
/// (<c>docs/identity-and-access.md</c> §4).
/// </summary>
public interface IAuditLog
{
    /// <summary>One page of the current organization's log, newest first, narrowed by the query's filters.</summary>
    /// <remarks>Nothing for somebody without <see cref="Permission.ReadAuditLog"/>.</remarks>
    Task<PagedResult<AuditRecord>> ReadAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>Who acted and which actions appear in the current organization's log, for the filters' choices.</summary>
    /// <remarks>Empty lists for somebody without <see cref="Permission.ReadAuditLog"/>.</remarks>
    Task<AuditFilterChoices> ReadFilterChoicesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Which page of the log, and what to narrow it to. A filter left <c>null</c> narrows nothing.</summary>
/// <param name="Page">The page, counting from one.</param>
/// <param name="PageSize">Entries per page, from 1 to <see cref="MaxPageSize"/>.</param>
/// <param name="Actor">The address the actor used, matched exactly, as the log holds it (one of <see cref="AuditFilterChoices.Actors"/>).</param>
/// <param name="Action">The stable action name, such as <c>member.role_changed</c>, matched exactly.</param>
/// <param name="From">The first day to include, in UTC.</param>
/// <param name="To">The last day to include, in UTC.</param>
public sealed record AuditQuery(
    int Page = 1,
    int PageSize = 50,
    string? Actor = null,
    string? Action = null,
    DateOnly? From = null,
    DateOnly? To = null)
{
    /// <summary>The most entries one page may hold.</summary>
    public const int MaxPageSize = 200;
}

/// <summary>The values the log's filters can take: those that occur in the log.</summary>
public sealed record AuditFilterChoices(IReadOnlyList<string> Actors, IReadOnlyList<string> Actions)
{
    /// <summary>No choices at all.</summary>
    public static AuditFilterChoices None { get; } = new([], []);
}
