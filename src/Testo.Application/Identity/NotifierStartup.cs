using Microsoft.Extensions.DependencyInjection;

namespace Testo.Application.Identity;

/// <summary>
/// Refuses to start a deployed application that would write invitation and password-reset tokens
/// to its log.
/// </summary>
/// <remarks>
/// <para>
/// There is no mail provider yet, so <see cref="LoggingInvitationNotifier"/> stands in and writes
/// the token where a developer can read it. That is right for a machine with no mail server and
/// wrong everywhere else: anyone who can read the log could accept any invitation or reset any
/// password (D-3 in <c>docs/security-register.md</c>).
/// </para>
/// <para>
/// The severity of that is zero while nothing is deployed and total on the day something is, which
/// is exactly the kind of defect that survives until it matters. This turns it into a startup
/// failure, which does not.
/// </para>
/// </remarks>
public static class NotifierStartup
{
    /// <summary>
    /// Throws outside development if links would go to the log rather than to a person.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="isDevelopment">Whether this is a developer's machine.</param>
    /// <exception cref="InvalidOperationException">Tokens would be written to the log.</exception>
    public static void EnsureLinksReachPeople(IServiceProvider services, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (isDevelopment)
        {
            return;
        }

        using var scope = services.CreateScope();

        if (scope.ServiceProvider.GetRequiredService<IInvitationNotifier>() is not LoggingInvitationNotifier)
        {
            return;
        }

        throw new InvalidOperationException(
            $"""
            Invitation and password-reset links would be written to the log.

            No mail provider is registered, so {nameof(LoggingInvitationNotifier)} is standing in. It
            writes the token in clear, which means anyone who can read the log can accept any
            invitation and reset any password. That is fine on a developer's machine and nowhere
            else, so this environment refuses to start.

            Register an {nameof(IInvitationNotifier)} that sends email, before
            AddTestoApplication, and it will be used instead.
            """);
    }
}
