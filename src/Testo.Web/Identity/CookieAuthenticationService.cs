using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Testo.Application.Identity;
using Testo.Contracts.Common;
using Testo.Contracts.Identity;
using IAuthenticationService = Testo.Contracts.Identity.IAuthenticationService;
using SignInResult = Testo.Contracts.Identity.SignInResult;

namespace Testo.Web.Identity;

/// <summary>
/// Turns the application's decision into a session.
/// </summary>
/// <remarks>
/// The application decides <em>whether</em> somebody may in; this decides <em>how</em> the
/// session is carried, because only the host may touch a cookie. That split is what keeps the
/// credential rules testable without an HTTP request, and why these methods only work from a
/// statically rendered page — an interactive session has no response to set a cookie on.
/// </remarks>
internal sealed class CookieAuthenticationService(
    IHttpContextAccessor accessor,
    SignInWorkflow signIn,
    PasswordResetWorkflow passwordReset)
    : IAuthenticationService
{
    /// <inheritdoc />
    public async Task<SignInResult> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        var context = RequireHttpContext();

        var (result, ticket) = await signIn
            .AuthenticateAsync(email, password, context.Connection.RemoteIpAddress?.ToString(), cancellationToken)
            .ConfigureAwait(false);

        switch (result.Outcome)
        {
            case SignInOutcome.Succeeded when ticket is not null:
                await IssueAsync(context, ticket, rememberMe).ConfigureAwait(false);
                break;

            case SignInOutcome.RequiresTwoFactor or SignInOutcome.RequiresTwoFactorSetup
                when result.UserId is { } userId:
                await HoldForSecondFactorAsync(context, userId).ConfigureAwait(false);
                break;

            default:
                break;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SignInResult> VerifyTwoFactorAsync(
        string code,
        bool rememberMachine,
        CancellationToken cancellationToken = default)
    {
        var context = RequireHttpContext();

        var held = await context.AuthenticateAsync(AuthenticationSchemes.TwoFactor).ConfigureAwait(false);

        if (held.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) is not { } value
            || !Guid.TryParse(value, out var userId))
        {
            // The interim cookie has gone — a long pause, or somebody arriving at this page
            // directly. Start again rather than guess.
            return new SignInResult(SignInOutcome.Failed);
        }

        var (result, ticket) = await signIn
            .VerifyCodeAsync(userId, code, context.Connection.RemoteIpAddress?.ToString(), cancellationToken)
            .ConfigureAwait(false);

        if (result.Outcome == SignInOutcome.Succeeded && ticket is not null)
        {
            await context.SignOutAsync(AuthenticationSchemes.TwoFactor).ConfigureAwait(false);
            await IssueAsync(context, ticket, rememberMachine).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var context = RequireHttpContext();

        await context.SignOutAsync(AuthenticationSchemes.Session).ConfigureAwait(false);
        await context.SignOutAsync(AuthenticationSchemes.TwoFactor).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var context = RequireHttpContext();

        return passwordReset.RequestAsync(email, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<OperationResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var context = RequireHttpContext();

        return passwordReset.ResetAsync(
            token,
            newPassword,
            context.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SignInResult> SwitchOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var context = RequireHttpContext();

        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } value
            || !Guid.TryParse(value, out var userId))
        {
            return new SignInResult(SignInOutcome.Failed);
        }

        var ticket = await signIn
            .TicketForOrganizationAsync(userId, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (ticket is null)
        {
            return new SignInResult(SignInOutcome.NoActiveMembership, userId);
        }

        await IssueAsync(context, ticket, rememberMe: false).ConfigureAwait(false);
        return new SignInResult(SignInOutcome.Succeeded, userId, organizationId);
    }

    private static Task IssueAsync(HttpContext context, SignInTicket ticket, bool rememberMe)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, ticket.UserId.ToString()),
                new Claim(ClaimTypes.Email, ticket.Email),
                new Claim(ClaimTypes.Name, ticket.DisplayName),
                new Claim(TestoClaims.Organization, ticket.OrganizationId.ToString()),
                new Claim(TestoClaims.OrganizationName, ticket.OrganizationName),
                new Claim(TestoClaims.Role, ticket.Role.ToString()),
                new Claim(TestoClaims.SecurityStamp, ticket.SecurityStamp),
            ],
            AuthenticationSchemes.Session);

        return context.SignInAsync(
            AuthenticationSchemes.Session,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = rememberMe });
    }

    /// <summary>
    /// A short-lived cookie that says only who is halfway in. It carries no role and no
    /// organization, so it opens nothing on its own.
    /// </summary>
    private static Task HoldForSecondFactorAsync(HttpContext context, Guid userId) =>
        context.SignInAsync(
            AuthenticationSchemes.TwoFactor,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                AuthenticationSchemes.TwoFactor)));

    private HttpContext RequireHttpContext() =>
        accessor.HttpContext
        ?? throw new InvalidOperationException(
            "Signing in needs an HTTP response to put the cookie on. Call this from a statically "
            + "rendered page, not from an interactive component.");
}
