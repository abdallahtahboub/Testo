using System.Collections.Concurrent;
using Testo.Application.Identity;

namespace Testo.Application.Tests.Support;

/// <summary>
/// Keeps the links instead of sending them, so a test can follow one the way a person would.
/// </summary>
public sealed class RecordingNotifier : IInvitationNotifier
{
    private readonly ConcurrentDictionary<string, string> _invitations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _resets = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task SendInvitationAsync(
        string email,
        string organizationName,
        string token,
        CancellationToken cancellationToken = default)
    {
        _invitations[email] = token;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        _resets[email] = token;
        return Task.CompletedTask;
    }

    /// <summary>The token in the invitation that address was sent.</summary>
    public string InvitationTokenFor(string email) =>
        _invitations.TryGetValue(email, out var token)
            ? token
            : throw new InvalidOperationException($"No invitation was sent to {email}.");

    /// <summary>The token in the password-reset link that address was sent.</summary>
    public string ResetTokenFor(string email) =>
        _resets.TryGetValue(email, out var token)
            ? token
            : throw new InvalidOperationException($"No password reset was sent to {email}.");

    /// <summary>Whether anything at all was sent to that address.</summary>
    public bool SentAnythingTo(string email) => _invitations.ContainsKey(email) || _resets.ContainsKey(email);
}
