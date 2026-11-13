using Testo.Application.Identity;

namespace Testo.E2E.Support;

/// <summary>
/// Stands in for a mail provider in the browser tests, and deliberately keeps nothing.
/// </summary>
/// <remarks>
/// The application refuses to start outside development if links would go to its log (D-3), and
/// these tests run it as a deployed site. So the test host registers a sender of its own. It
/// discards rather than records, because no browser test follows an invitation link; the tests
/// that do that live in <c>Testo.Application.Tests</c> and use a recording one.
/// </remarks>
internal sealed class SilentNotifier : IInvitationNotifier
{
    public Task SendInvitationAsync(
        string email,
        string organizationName,
        string token,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
