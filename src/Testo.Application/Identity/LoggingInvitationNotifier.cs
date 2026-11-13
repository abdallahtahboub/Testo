using Microsoft.Extensions.Logging;

namespace Testo.Application.Identity;

/// <summary>
/// Writes the link to the log instead of sending it, so the flows can be walked end to end before
/// a mail provider is chosen. Not for production, and the warning says so every time.
/// </summary>
internal sealed class LoggingInvitationNotifier(ILogger<LoggingInvitationNotifier> logger) : IInvitationNotifier
{
    /// <inheritdoc />
    public Task SendInvitationAsync(
        string email,
        string organizationName,
        string token,
        CancellationToken cancellationToken = default)
    {
        IdentityLog.InvitationNotSent(logger, organizationName, email, token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        IdentityLog.PasswordResetNotSent(logger, email, token);
        return Task.CompletedTask;
    }
}
