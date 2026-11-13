namespace Testo.Application.Audit;

/// <summary>
/// The names of the things worth recording. Constants rather than free text, so that a query for
/// "every time somebody's role changed" finds all of them.
/// </summary>
internal static class AuditActions
{
    public const string SignedIn = "user.signed_in";
    public const string SignInFailed = "user.sign_in_failed";
    public const string SignedOut = "user.signed_out";
    public const string PasswordResetRequested = "user.password_reset_requested";
    public const string PasswordReset = "user.password_reset";
    public const string TwoFactorEnabled = "user.two_factor_enabled";
    public const string TwoFactorDisabled = "user.two_factor_disabled";

    public const string MemberInvited = "member.invited";
    public const string InvitationRevoked = "member.invitation_revoked";
    public const string MemberJoined = "member.joined";
    public const string MemberRoleChanged = "member.role_changed";
    public const string MemberRemoved = "member.removed";
}
