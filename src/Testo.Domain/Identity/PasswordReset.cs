using Testo.Domain.Common;

namespace Testo.Domain.Identity;

/// <summary>
/// A pending "I have forgotten my password". Single use, short-lived, and held only as a hash —
/// the same shape as <see cref="Invitation"/>, and for the same reason: a stolen database backup
/// must not be a set of working links.
/// </summary>
/// <remarks>
/// This is ours rather than one of ASP.NET Core Identity's data-protection tokens. Those are
/// stateless, which means they cannot be withdrawn and cannot be made single use, and they tie the
/// data-protection key ring to something a person's account security depends on. A row can be
/// cancelled, counted, and audited.
/// </remarks>
public sealed class PasswordReset : Entity
{
    /// <summary>Whose password is being reset.</summary>
    public Guid UserId { get; set; }

    /// <summary>The account.</summary>
    public User? User { get; set; }

    /// <summary>The SHA-256 hash of the token in the link.</summary>
    public required byte[] TokenHash { get; set; }

    /// <summary>When the link stops working.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When it was used, if it was. A link works once.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Where the request came from, so a flood of them can be recognised.</summary>
    public string? RequestedFromIp { get; set; }

    /// <summary>Whether the link can still be used at the given moment.</summary>
    public bool IsUsableAt(DateTimeOffset moment) => UsedAt is null && ExpiresAt > moment;
}
