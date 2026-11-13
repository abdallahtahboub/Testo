using System.Globalization;
using System.Threading.RateLimiting;
using Testo.Platform;

namespace Testo.Web.Security;

/// <summary>
/// Limits how often one address may post to the account pages.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/identity-and-access.md</c> §5 promises a rate limit per address <em>and per IP</em>.
/// Only the per-account lockout existed, so one machine could try one likely password against
/// every account in the product without friction, which is how credential stuffing actually works
/// (D-4 in <c>docs/security-register.md</c>).
/// </para>
/// <para>
/// Only unsafe methods are limited. A <c>GET</c> of the sign-in page is somebody arriving, and an
/// office behind one address arrives all morning; a <c>POST</c> is an attempt. That split is what
/// keeps the limit from becoming a denial of service against a customer whose staff share an
/// address.
/// </para>
/// <para>
/// <strong>Behind a reverse proxy this needs forwarded headers.</strong> The partition is the
/// connection's remote address, which is the proxy's unless the deployment configures
/// <c>UseForwardedHeaders</c> with its known proxies. Without that the limit still works, but as
/// one bucket for everybody — safe, and far blunter than intended.
/// </para>
/// </remarks>
public static class AccountRateLimiting
{
    /// <summary>How many posts one address may make per minute. Configurable so tests can be strict.</summary>
    public const string PermitsPerMinuteKey = "Security:AccountPostsPerMinute";

    /// <summary>
    /// Thirty a minute: useless to somebody working through a password list, invisible to a floor
    /// of people signing in at nine o'clock.
    /// </summary>
    public const int DefaultPermitsPerMinute = 30;

    /// <summary>Adds the limiter. Nothing is limited until <see cref="UseAccountRateLimiting"/> runs.</summary>
    public static IServiceCollection AddAccountRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var permits = configuration.GetValue(PermitsPerMinuteKey, DefaultPermitsPerMinute);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsAnAttempt(context)
                    ? RateLimitPartition.GetSlidingWindowLimiter(
                        AddressOf(context),
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = permits,
                            Window = TimeSpan.FromMinutes(1),

                            // Six segments, so the allowance returns gradually over the minute
                            // rather than all at once on a boundary an attacker could wait for.
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("not-an-attempt"));

            options.OnRejected = (context, cancellationToken) =>
            {
                // Say when to come back, so a person who shares an office address is not left
                // guessing. It tells an attacker nothing they could not measure anyway.
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)TimeSpan.FromMinutes(1).TotalSeconds).ToString(CultureInfo.InvariantCulture);

                return ValueTask.CompletedTask;
            };
        });

        return services;
    }

    /// <summary>Turns the limiter on. Early, so a refused attempt costs as little as possible.</summary>
    public static IApplicationBuilder UseAccountRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseRateLimiter();
    }

    /// <summary>A post to an account page: signing in, resetting, accepting an invitation.</summary>
    private static bool IsAnAttempt(HttpContext context) =>
        !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && context.Request.Path.StartsWithSegments(Routes.Account, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Where the attempt came from. Requests with no address share one bucket rather than escaping
    /// the limit.
    /// </summary>
    private static string AddressOf(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
