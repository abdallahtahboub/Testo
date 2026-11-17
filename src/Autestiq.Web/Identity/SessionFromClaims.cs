using System.Security.Claims;
using Autestiq.Contracts.Identity;
using Autestiq.UI.Shared.Identity;

namespace Autestiq.Web.Identity;

/// <summary>
/// Fills <see cref="SignedInUser"/> from the claims of the session cookie, for every request.
/// </summary>
/// <remarks>
/// <para>
/// The current user and the tenant filter read the organization from <see cref="SignedInUser"/>. It used to be filled
/// only while a page rendered (<c>App.razor</c>), so an endpoint that is not a page — the screenshot links, the test
/// download — saw nobody signed in: the tenant filter then matched no organization and every screenshot answered
/// "not found". Filled here, once per request, after authentication has checked the cookie, every endpoint sees the
/// same person a page would.
/// </para>
/// <para>An interactive session has no request; there the values carried over from the prerendered page are used.</para>
/// </remarks>
internal static class SessionFromClaims
{
    /// <summary>Copies the signed-in person's claims, when there is one.</summary>
    public static void Fill(SignedInUser session, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(user);

        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        session.UserId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        session.Email = user.FindFirstValue(ClaimTypes.Email);
        session.DisplayName = user.FindFirstValue(ClaimTypes.Name);
        session.OrganizationName = user.FindFirstValue(AutestiqClaims.OrganizationName);
        session.OrganizationId = Guid.TryParse(user.FindFirstValue(AutestiqClaims.Organization), out var organization) ? organization : null;
        session.Role = Enum.TryParse<Role>(user.FindFirstValue(AutestiqClaims.Role), out var role) ? role : null;
    }

    /// <summary>Fills the request's <see cref="SignedInUser"/> before any endpoint runs.</summary>
    public static IApplicationBuilder UseSessionFromClaims(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            Fill(context.RequestServices.GetRequiredService<SignedInUser>(), context.User);
            await next(context).ConfigureAwait(false);
        });
}
