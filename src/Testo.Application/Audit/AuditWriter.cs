using System.Text.Json;
using Testo.Domain.Audit;
using Testo.Persistence;

namespace Testo.Application.Audit;

/// <summary>
/// Writes the log of what happened. Internal on purpose: an entry is written by the use case that
/// causes it, so one cannot be forgotten by a caller or forged by one
/// (<c>docs/identity-and-access.md</c> §4).
/// </summary>
/// <remarks>
/// The entry is added to the same transaction as the change it describes. Either both are saved
/// or neither is, so the log cannot claim something that did not happen, nor miss something that
/// did.
/// </remarks>
internal sealed class AuditWriter(TestoDbContext database, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    /// <summary>
    /// Adds an entry. It is saved when the caller saves, not before.
    /// </summary>
    /// <param name="organizationId">Which organization this happened in.</param>
    /// <param name="actorUserId">Who did it, or <c>null</c> when the system did.</param>
    /// <param name="actorEmail">The address used, kept even when no account was found.</param>
    /// <param name="action">A stable name such as <c>member.role_changed</c>.</param>
    /// <param name="subjectType">The kind of object it happened to.</param>
    /// <param name="subjectId">Which object.</param>
    /// <param name="subjectName">A readable name, so the line still means something later.</param>
    /// <param name="ipAddress">Where the request came from.</param>
    /// <param name="before">The values before, or <c>null</c> when the object was created.</param>
    /// <param name="after">The values after, or <c>null</c> when it was destroyed.</param>
    public void Record(
        Guid organizationId,
        Guid? actorUserId,
        string? actorEmail,
        string action,
        string subjectType,
        Guid? subjectId = null,
        string? subjectName = null,
        string? ipAddress = null,
        object? before = null,
        object? after = null) =>
        database.AuditEntries.Add(new AuditEntry
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = action,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectName = subjectName,
            At = timeProvider.GetUtcNow(),
            IpAddress = ipAddress,
            Before = before is null ? null : JsonSerializer.Serialize(before, Compact),
            After = after is null ? null : JsonSerializer.Serialize(after, Compact),
        });
}
