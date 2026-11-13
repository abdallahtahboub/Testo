using Testo.Contracts.Identity;
using Testo.Domain.Identity;

namespace Testo.Application.Tests.Support;

/// <summary>
/// Who the test is pretending to be. Changing <see cref="OrganizationId"/> is how a test becomes
/// another customer and asks whether it can see the first one's rows.
/// </summary>
public sealed class TestPrincipal : ICurrentUser, IOrganizationContext
{
    /// <inheritdoc cref="IOrganizationContext.OrganizationId" />
    public Guid? OrganizationId { get; set; }

    /// <inheritdoc />
    public Guid? UserId { get; set; }

    /// <inheritdoc />
    public string? Email { get; set; }

    /// <inheritdoc />
    public string? DisplayName { get; set; }

    /// <inheritdoc />
    public Role? Role { get; set; }

    /// <inheritdoc />
    public bool IsSignedIn => UserId is not null;

    /// <inheritdoc />
    public bool Can(Permission permission) =>
        Role is { } role && Permissions.Allows(role, permission);

    /// <inheritdoc />
    public Guid RequireOrganizationId() =>
        OrganizationId ?? throw new InvalidOperationException("The test set no organization.");

    /// <summary>Become this person, in this organization.</summary>
    public void Become(Guid organizationId, Guid userId, Role role, string email = "tester@example.com")
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        Email = email;
        DisplayName = email;
    }
}
