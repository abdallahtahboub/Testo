namespace Testo.Application.Identity;

/// <summary>
/// Sends the links that invitations and password resets depend on.
/// </summary>
/// <remarks>
/// There is no mail provider yet: choosing one, and the records that go with it in an EU
/// processor list, is its own piece of work. Until then the development implementation writes the
/// link to the log, which is enough to invite somebody, accept, and sign in. Nothing else in the
/// product has to change when a real sender arrives — only this registration.
/// </remarks>
public interface IInvitationNotifier
{
    /// <summary>Tells somebody they have been invited, and how to accept.</summary>
    Task SendInvitationAsync(
        string email,
        string organizationName,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Sends somebody a link to choose a new password.</summary>
    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default);
}
