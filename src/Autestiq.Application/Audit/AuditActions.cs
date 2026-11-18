namespace Autestiq.Application.Audit;

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

    public const string RunStarted = "run.started";
    public const string RunCancelled = "run.cancelled";
    public const string RunFinished = "run.finished";

    public const string ProjectCreated = "project.created";
    public const string EnvironmentAdded = "environment.added";
    public const string EnvironmentRemoved = "environment.removed";
    public const string TestDescribed = "test.described";
    public const string TestPlanned = "test.planned";
    public const string TestApproved = "test.approved";
    public const string TestApprovalWithdrawn = "test.approval_withdrawn";
    public const string TestDownloaded = "test.downloaded";
    public const string EvidenceRetentionChanged = "project.evidence_retention_changed";
    public const string EvidenceExpired = "evidence.expired";
    public const string ExplorationActingAllowed = "environment.exploration_acting_allowed";
    public const string ExplorationActingWithdrawn = "environment.exploration_acting_withdrawn";
    public const string SecretSet = "environment.secret_set";
    public const string SecretDeleted = "environment.secret_deleted";
    public const string AiProcessingAllowed = "project.ai_processing_allowed";
    public const string AiProcessingWithdrawn = "project.ai_processing_withdrawn";
    public const string ProductionAuthorized = "environment.production_authorized";
    public const string ProductionWithdrawn = "environment.production_withdrawn";

    /// <summary>An operator moved the organization to another plan version (ADR-0028).</summary>
    public const string PlanChanged = "organization.plan_changed";
}
