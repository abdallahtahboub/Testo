using Testo.Contracts.Identity;

namespace Testo.Application.Identity;

/// <summary>
/// Everything the host needs to issue a session, once the credentials have been accepted.
/// </summary>
/// <remarks>
/// The application decides <em>whether</em> somebody may in; the host decides <em>how</em> the
/// session is carried, because only it may touch a cookie. This record is the hand-over, and is
/// why the credential logic can be tested without an HTTP request.
/// </remarks>
/// <param name="UserId">The person.</param>
/// <param name="Email">Their address.</param>
/// <param name="DisplayName">The name to show.</param>
/// <param name="OrganizationId">The organization they are signing in to.</param>
/// <param name="OrganizationName">Its name, for the top bar.</param>
/// <param name="Role">What they may do there.</param>
/// <param name="SecurityStamp">
/// The value every request re-checks. Changing it on the account ends every session that person
/// has, which is what makes removing somebody take effect at once.
/// </param>
/// <param name="Locale">Their language, so the first page after sign-in is already in it.</param>
public sealed record SignInTicket(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid OrganizationId,
    string OrganizationName,
    Role Role,
    string SecurityStamp,
    string Locale);
