using Testo.Contracts.Common;

namespace Testo.Contracts.Identity;

/// <summary>
/// Signing in and out, and the things that go wrong around it. The product depends on this and
/// never on ASP.NET Core Identity, so a per-organization identity provider can be added later as
/// a second implementation rather than a rewrite (ADR-0013).
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Checks an address and password and, when they are right and no second factor is due, issues
    /// the session.
    /// </summary>
    /// <remarks>
    /// The sign-in screen asks for the address on its own first, which is what later lets the
    /// address decide whether this organization signs in here or at its own provider.
    /// </remarks>
    Task<SignInResult> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default);

    /// <summary>Completes a sign-in that was waiting for a time-based one-time code.</summary>
    Task<SignInResult> VerifyTwoFactorAsync(
        string code,
        bool rememberMachine,
        CancellationToken cancellationToken = default);

    /// <summary>Ends the current session.</summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a password reset, and says nothing about whether the address has an account.
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finishes a password reset. Succeeding changes the security stamp, which ends every session
    /// that person had.
    /// </summary>
    Task<OperationResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Switches the current session to another organization the person belongs to.</summary>
    Task<SignInResult> SwitchOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
