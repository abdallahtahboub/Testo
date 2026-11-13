namespace Testo.Contracts.Identity;

/// <summary>How a sign-in attempt ended.</summary>
/// <remarks>
/// A wrong password and an address with no account both give <see cref="Failed"/>, and take the
/// same time, so that the screen cannot be used to find out who has an account
/// (<c>docs/identity-and-access.md</c> §5).
/// </remarks>
public enum SignInOutcome
{
    /// <summary>The person is signed in.</summary>
    Succeeded = 1,

    /// <summary>The password was right; the second factor is still to come.</summary>
    RequiresTwoFactor = 2,

    /// <summary>
    /// The organization requires a second factor and this person has not set one up yet. They are
    /// sent to set it up before they reach anything else.
    /// </summary>
    RequiresTwoFactorSetup = 3,

    /// <summary>The address or the password was wrong. Which one is deliberately not said.</summary>
    Failed = 4,

    /// <summary>Too many attempts. The person is told, and told how to recover — never silently.</summary>
    LockedOut = 5,

    /// <summary>The credentials were right, but the person belongs to no active organization.</summary>
    NoActiveMembership = 6,
}

/// <summary>The result of a sign-in attempt.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="UserId">The person, when the credentials were right.</param>
/// <param name="OrganizationId">The organization they were signed in to, when they were.</param>
/// <param name="LockoutEnds">When a lockout lifts, so the screen can say so.</param>
public sealed record SignInResult(
    SignInOutcome Outcome,
    Guid? UserId = null,
    Guid? OrganizationId = null,
    DateTimeOffset? LockoutEnds = null)
{
    /// <summary>Whether the person is now fully signed in.</summary>
    public bool IsSignedIn => Outcome == SignInOutcome.Succeeded;
}
