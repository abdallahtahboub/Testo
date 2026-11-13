namespace Testo.Domain.Common;

/// <summary>
/// What every stored record carries: an identity, and a record of who wrote it and when
/// (<c>docs/domain-model.md</c> §2).
/// </summary>
/// <remarks>
/// The identifier is a UUID version 7: random, so it reveals nothing in a URL, but ordered by
/// creation time, so it does not fragment the index the way a version 4 UUID does. Sequential
/// integers are deliberately not used; they leak how many records and customers exist.
/// </remarks>
public abstract class Entity
{
    /// <summary>The primary key. Assigned once, when the object is first created.</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>When the record was created. Set by the persistence layer, never by hand.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The user who created the record, or <c>null</c> for records the system itself wrote.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>When the record last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The user who last changed the record.</summary>
    public Guid? UpdatedBy { get; set; }
}
