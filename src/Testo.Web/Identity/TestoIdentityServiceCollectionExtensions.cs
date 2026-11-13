using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testo.Application.Identity;
using Testo.Contracts.Identity;
using Testo.Platform;
using IAuthenticationService = Testo.Contracts.Identity.IAuthenticationService;

namespace Testo.Web.Identity;

/// <summary>Wires sign-in into the host.</summary>
public static class TestoIdentityServiceCollectionExtensions
{
    /// <summary>How long a session lasts without being used (<c>docs/identity-and-access.md</c> §1).</summary>
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>How long a remembered session lasts at the outside, used or not.</summary>
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Configuration key that lets a host serve the session cookie over plain HTTP.
    /// </summary>
    /// <remarks>
    /// Only the browser tests set this. They run the real application on a loopback port without
    /// a certificate, and a cookie marked <c>Secure</c> would never be stored, so every test
    /// would show a person who cannot sign in for a reason the product does not have.
    /// </remarks>
    public const string AllowInsecureCookiesKey = "Security:AllowInsecureSessionCookie";

    /// <summary>Adds the session cookies and the services that read them.</summary>
    /// <param name="services">The container.</param>
    /// <param name="allowInsecureCookie">
    /// <c>true</c> only where there is no HTTPS, which in this repository means the browser
    /// tests. Everywhere else the cookie is <c>Secure</c>.
    /// </param>
    public static IServiceCollection AddTestoIdentity(this IServiceCollection services, bool allowInsecureCookie = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        var securePolicy = allowInsecureCookie ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddHttpContextAccessor();
        services.AddCascadingAuthenticationState();

        // One object answers both, because both are answered from the same session.
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddScoped<IOrganizationContext>(provider => provider.GetRequiredService<CurrentUser>());

        services.TryAddScoped<IAuthenticationService, CookieAuthenticationService>();

        services.AddAuthentication(AuthenticationSchemes.Session)
            .AddCookie(AuthenticationSchemes.Session, options =>
            {
                options.Cookie.Name = AuthenticationSchemes.Session;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = securePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;

                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = true;
                options.Cookie.MaxAge = AbsoluteLifetime;

                options.LoginPath = Routes.SignIn;
                options.LogoutPath = Routes.SignOut;
                options.ReturnUrlParameter = "next";

                options.Events.OnValidatePrincipal = ValidateAsync;
            })
            .AddCookie(AuthenticationSchemes.TwoFactor, options =>
            {
                options.Cookie.Name = AuthenticationSchemes.TwoFactor;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = securePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;

                // Long enough to read a code off a phone, short enough not to be worth stealing.
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                options.SlidingExpiration = false;
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Checks on every request that the session is still one the product would issue today.
    /// </summary>
    /// <remarks>
    /// This is what makes removing somebody take effect at once rather than at the next expiry.
    /// It costs one indexed read per request, which is the price of that promise.
    /// </remarks>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;

        if (principal?.FindFirstValue(ClaimTypes.NameIdentifier) is not { } user
            || !Guid.TryParse(user, out var userId)
            || principal.FindFirstValue(TestoClaims.Organization) is not { } organization
            || !Guid.TryParse(organization, out var organizationId)
            || principal.FindFirstValue(TestoClaims.SecurityStamp) is not { } stamp)
        {
            context.RejectPrincipal();
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<SessionValidator>();

        if (!await validator.IsStillValidAsync(userId, organizationId, stamp, context.HttpContext.RequestAborted)
            .ConfigureAwait(false))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthenticationSchemes.Session).ConfigureAwait(false);
        }
    }
}
