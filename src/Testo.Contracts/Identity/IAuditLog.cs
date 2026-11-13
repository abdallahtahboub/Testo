using Testo.Contracts.Common;

namespace Testo.Contracts.Identity;

/// <summary>
/// Reading what happened. Writing is not on this interface on purpose: entries are written by the
/// use cases that cause them, so an entry cannot be forgotten or forged from outside
/// (<c>docs/identity-and-access.md</c> §4).
/// </summary>
public interface IAuditLog
{
    /// <summary>One page of the current organization's log, newest first.</summary>
    Task<PagedResult<AuditRecord>> ReadAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
