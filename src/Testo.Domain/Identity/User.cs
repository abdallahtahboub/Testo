using Testo.Domain.Common;

namespace Testo.Domain.Identity;

/// <summary>
/// A person's account. Deliberately <em>outside</em> the tenant boundary: one account, one
/// password, possibly several organizations — a consultant working for two customers, or somebody
/// who changes employer (<c>docs/domain-model.md</c> §1).
/// </summary>
/// <remarks>
/// The credential fields are ours, not ASP.NET Core Identity's. Identity is the mechanism that
/// hashes and verifies, reached through a store written over this entity, so the domain keeps no
/// reference to it (ADR-0013).
/// </remarks>
public sealed class User : Entity
{
    /// <summary>The address the person signs in with. Unique across the product.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// The upper-case form of <see cref="Email"/>, which lookups and the unique index use, so that
    /// case never decides whether two people are the same person.
    /// </summary>
    public required string NormalizedEmail { get; set; }

    /// <summary>Whether the address has been proven to belong to them.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>The name shown next to their work. Never their email address.</summary>
    public required string DisplayName { get; set; }

    /// <summary>The hashed password. <c>null</c> while an invitation is still open.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Changed whenever the credentials change, which is what lets every existing session be
    /// ended at once (<c>docs/identity-and-access.md</c> §3).
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.CreateVersion7().ToString("N");

    /// <summary>Guards against two concurrent edits of the same account.</summary>
    public string ConcurrencyStamp { get; set; } = Guid.CreateVersion7().ToString("N");

    /// <summary>Whether this person uses a second factor.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>The TOTP shared secret, set when the second factor is turned on.</summary>
    public string? AuthenticatorKey { get; set; }

    /// <summary>When the account stops being locked after too many failed attempts.</summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Failed sign-in attempts since the last success.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>Their preferred language, <c>en</c> or <c>de</c>.</summary>
    public string Locale { get; set; } = "en";

    /// <summary>Their preferred theme: <c>light</c>, <c>dark</c> or <c>system</c>.</summary>
    public string Theme { get; set; } = "system";

    /// <summary>The organizations they belong to.</summary>
    public ICollection<Membership> Memberships { get; } = [];
}
