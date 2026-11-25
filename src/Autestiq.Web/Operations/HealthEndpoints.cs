using Autestiq.Application.Operations;

namespace Autestiq.Web.Operations;

/// <summary>
/// Liveness and readiness for whatever runs the application (Milestone 5.1 R2). Anonymous, because a load balancer
/// has no account, and so they say only which checks failed, by name: the reasons, which can name servers and
/// directories, go to the log.
/// </summary>
public static partial class HealthEndpoints
{
    /// <summary>Answers while the process can answer.</summary>
    public const string Live = "/health/live";

    /// <summary>Answers 200 only when the application can serve: database, schema, key ring, worker image.</summary>
    public const string Ready = "/health/ready";

    /// <summary>Adds both endpoints.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Live, (HttpContext context) =>
        {
            NoStore(context);
            return Results.Text("alive");
        }).AllowAnonymous();

        endpoints.MapGet(Ready, ReadyAsync).AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ReadyAsync(HttpContext context, Readiness readiness, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        NoStore(context);

        var checks = await readiness.CheckAsync(cancellationToken).ConfigureAwait(false);
        var failed = checks.Where(check => !check.Ready).ToList();

        if (failed.Count == 0)
        {
            return Results.Text("ready");
        }

        var logger = loggers.CreateLogger(typeof(HealthEndpoints));

        foreach (var check in failed)
        {
            NotReady(logger, check.Name, check.Reason ?? "");
        }

        return Results.Text("not ready: " + string.Join(", ", failed.Select(check => check.Name)), statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Not ready: {Check}: {Reason}")]
    private static partial void NotReady(ILogger logger, string check, string reason);
}
