using System.Net;
using Autestiq.Contracts.Execution;

namespace Autestiq.Worker;

/// <summary>
/// Decides what an exploration may do to an application beyond reading it.
/// </summary>
/// <remarks>
/// <para>
/// Pressing a control is an action on an application, and typing into a form and sending it is a
/// larger one. Somebody else's application is never acted on without their approval, and nothing in
/// this product can record that approval yet (ADR-0021, ADR-0022). So both are allowed only on an
/// application we run ourselves, and the rule is here, in one place, with a test for each condition,
/// rather than left to whoever builds the next caller.
/// </para>
/// <para>
/// "An application we run ourselves" is two things at once: the job names the target as trusted-local,
/// and the target is a loopback address written as a number. The second condition is what holds when
/// the first is wrong. A caller that names a customer's origin as trusted, which the run dispatcher
/// would do today for a stored target (R-11 in <c>docs/security-register.md</c>), still cannot have a
/// form sent on an application that is somewhere on the internet.
/// </para>
/// </remarks>
public static class ExplorationPolicy
{
    /// <summary>Whether the job's target is an application we run ourselves.</summary>
    public static bool IsOurOwn(WorkerJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        // A name is not enough, not even "localhost": a name resolves to whatever its owner says.
        if (!Uri.TryCreate(job.TargetOrigin, UriKind.Absolute, out var target)
            || !IPAddress.TryParse(target.DnsSafeHost, out var address)
            || !IPAddress.IsLoopback(address))
        {
            return false;
        }

        return job.TrustedLocalOrigins.Any(origin =>
            string.Equals(origin.TrimEnd('/'), job.TargetOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether this job may press the application's controls while exploring.</summary>
    public static bool MayPressControls(WorkerJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.Task == WorkerTask.Explore && job.ExploreStates && IsOurOwn(job);
    }

    /// <summary>
    /// Whether this job may also type fixed test values into forms, and press the controls that are
    /// named for sending one.
    /// </summary>
    public static bool MaySubmitForms(WorkerJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.SubmitForms && MayPressControls(job);
    }
}
