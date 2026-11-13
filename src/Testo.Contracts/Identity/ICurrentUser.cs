namespace Testo.Contracts.Identity;

/// <summary>
/// Who is asking. The one place the rest of the product learns about the signed-in person, so
/// that swapping ASP.NET Core Identity for an external provider later changes an implementation
/// and nothing else (ADR-0013).
/// </summary>
/// <remarks>
/// <see cref="Can"/> answers from the role the session was issued with. The interface uses it to
/// hide what a person cannot use, but hiding is not enforcing: the server checks again on every
/// request that changes something (<c>docs/identity-and-access.md</c> §2).
/// </remarks>
public interface ICurrentUser
{
    /// <summary>Whether anybody is signed in at all.</summary>
    bool IsSignedIn { get; }

    /// <summary>The signed-in person, or <c>null</c> on a public page.</summary>
    Guid? UserId { get; }

    /// <summary>Their address, or <c>null</c> when nobody is signed in.</summary>
    string? Email { get; }

    /// <summary>The name to show next to their work, or <c>null</c> when nobody is signed in.</summary>
    string? DisplayName { get; }

    /// <summary>Their role in the organization they are currently in, if any.</summary>
    Role? Role { get; }

    /// <summary>Whether their role carries a permission.</summary>
    bool Can(Permission permission);
}
