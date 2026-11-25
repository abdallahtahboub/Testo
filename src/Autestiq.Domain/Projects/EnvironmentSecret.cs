using Autestiq.Domain.Common;

namespace Autestiq.Domain.Projects;

/// <summary>
/// One value of a test account's sign-in details for an environment, such as its password, kept encrypted (ADR-0033).
/// </summary>
/// <remarks>
/// Only the ciphertext is stored. It is encrypted under a purpose naming the organization and the environment, with
/// a key ring kept outside the database, so neither a copy of this table nor another environment's purpose reads it.
/// No service returns the value; it is decrypted only when a run starts, for the worker.
/// </remarks>
public sealed class EnvironmentSecret : Entity, IBelongsToOrganization
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The environment it signs in to.</summary>
    public Guid EnvironmentId { get; set; }

    /// <summary>The name a plan types it by, such as <c>password</c>: lower case letters, digits and underscores.</summary>
    public required string Name { get; set; }

    /// <summary>The value, encrypted.</summary>
    public required string Ciphertext { get; set; }

    /// <summary>Who set it last.</summary>
    public Guid? SetBy { get; set; }
}
