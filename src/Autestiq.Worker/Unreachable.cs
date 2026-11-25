namespace Autestiq.Worker;

/// <summary>
/// Whether a failed run failed because its own site could not be reached (R3 rehearsal finding F5, the owner's decision
/// of 2026-11-25): a request to the site's origin refused by the egress proxies, or a browser error that no connection
/// was made. Then the run has learnt nothing about the application and ends with no verdict, saying why, instead of
/// "Failed", which would blame the site for our network or for its being down.
/// </summary>
/// <remarks>
/// Only the run's own origin counts: a refused request to another origin (an analytics script, a font host) leaves a
/// failure a failure, because the application was reached and judged.
/// </remarks>
public static class Unreachable
{
    /// <summary>The browser's errors that mean no connection to the site was made, or none answered.</summary>
    private static readonly string[] NoConnection =
    [
        "net::ERR_CONNECTION_REFUSED", "net::ERR_CONNECTION_RESET", "net::ERR_CONNECTION_CLOSED", "net::ERR_EMPTY_RESPONSE",
        "net::ERR_NAME_NOT_RESOLVED", "net::ERR_ADDRESS_UNREACHABLE", "net::ERR_CONNECTION_TIMED_OUT", "net::ERR_TIMED_OUT",
        "net::ERR_TUNNEL_CONNECTION_FAILED", "net::ERR_PROXY_CONNECTION_FAILED", "net::ERR_INTERNET_DISCONNECTED",
    ];

    /// <summary>What shows the site was not reached, or <c>null</c> when it was.</summary>
    public static string? Of(string targetOrigin, IReadOnlyList<string> refusals, string? failure)
    {
        ArgumentNullException.ThrowIfNull(refusals);

        if (Uri.TryCreate(targetOrigin, UriKind.Absolute, out var target))
        {
            // The proxies write a refusal as "scheme://host:port — why".
            var origin = $"{target.Scheme}://{target.Host}:{target.Port} ";
            var tunnel = $"https://{target.Host}:{target.Port} ";

            if (refusals.FirstOrDefault(refusal => refusal.StartsWith(origin, StringComparison.OrdinalIgnoreCase)
                || refusal.StartsWith(tunnel, StringComparison.OrdinalIgnoreCase)) is { } refused)
            {
                return refused;
            }
        }

        return failure is not null && NoConnection.FirstOrDefault(error => failure.Contains(error, StringComparison.Ordinal)) is { } error2
            ? error2
            : null;
    }

    /// <summary>Why the run has no verdict.</summary>
    public static string Explain(string what) =>
        $"The site could not be reached ({what}), so nothing is known about the application: this is not a failure of it. "
        + "Check that the address is right, that the site is up, and that it is reachable from the internet.";
}
