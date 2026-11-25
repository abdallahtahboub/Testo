using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Autestiq.Application.Resources;

namespace Autestiq.Application.Identity;

/// <summary>
/// How invitation and password-reset email leaves the product: any provider's SMTP relay (Milestone 5.1 R2, ADR-0040).
/// Choosing a provider is configuration only. The password comes from the environment, never from a file in the
/// repository.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "Email:Smtp";

    /// <summary>The relay's host name. Empty means no email is configured.</summary>
    public string Host { get; set; } = "";

    /// <summary>The relay's port: 587 for submission with STARTTLS.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Whether to upgrade the connection with STARTTLS. Off only for a relay on this machine.</summary>
    public bool StartTls { get; set; } = true;

    /// <summary>The account name at the relay, if it needs one.</summary>
    public string? User { get; set; }

    /// <summary>The account's password, from the environment (<c>Email__Smtp__Password</c>).</summary>
    public string? Password { get; set; }

    /// <summary>The address mail is sent from.</summary>
    public string From { get; set; } = "";

    /// <summary>The address the links in mail point at, such as <c>https://app.example</c>.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Why these options cannot send mail, or <c>null</c> when they can.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            return "Email:Smtp:Host is empty.";
        }

        if (!MailAddress.TryCreate(From, out _))
        {
            return "Email:Smtp:From is not an email address.";
        }

        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
        {
            return "Email:Smtp:PublicBaseUrl is not an absolute http or https address.";
        }

        // A link carrying a token over plain http would travel readable, except to this machine.
        if (url.Scheme == "http" && !url.IsLoopback)
        {
            return "Email:Smtp:PublicBaseUrl must be https unless it points at this machine.";
        }

        // Mail and its links over a connection that is not encrypted, except to a relay on this machine.
        var onThisMachine = Host == "localhost" || (IPAddress.TryParse(Host, out var address) && IPAddress.IsLoopback(address));

        if (!StartTls && !onThisMachine)
        {
            return "Email:Smtp:StartTls is off for a relay that is not on this machine.";
        }

        return Port is > 0 and < 65536 ? null : "Email:Smtp:Port is not a port.";
    }

    /// <summary>The absolute address of a page with a token, such as the invitation's acceptance page.</summary>
    public Uri Link(string path, string token) =>
        new(new Uri(PublicBaseUrl.TrimEnd('/') + "/"), path.TrimStart('/') + "?token=" + Uri.EscapeDataString(token));
}

/// <summary>Registers the SMTP sender in place of the development one that writes to the log.</summary>
public static class SmtpEmailServiceCollectionExtensions
{
    /// <summary>
    /// Uses <paramref name="options"/> to send invitations and password resets. Call before
    /// <c>AddAutestiqApplication</c>, whose development sender is registered only if none is.
    /// </summary>
    /// <exception cref="InvalidOperationException">The options cannot send mail.</exception>
    public static IServiceCollection AddAutestiqSmtpEmail(this IServiceCollection services, SmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Problem() is { } problem)
        {
            throw new InvalidOperationException("Email is configured but cannot be sent: " + problem);
        }

        services.AddSingleton(options);
        services.AddScoped<IInvitationNotifier, SmtpInvitationNotifier>();
        return services;
    }
}

/// <summary>
/// Sends invitations and password resets by SMTP, in English and German together, since a person's language is not
/// known before they sign in. The token appears only in the link inside the message, never in a log line.
/// </summary>
public sealed partial class SmtpInvitationNotifier(SmtpOptions options, ILogger<SmtpInvitationNotifier> logger) : IInvitationNotifier
{
    private static readonly ResourceManager Texts = new(typeof(EmailResources));

    /// <summary>Where an invitation is accepted. The same as <c>Platform.Routes.AcceptInvitation</c>, which a test holds.</summary>
    public const string AcceptPath = "/account/accept";

    /// <summary>Where a new password is chosen. The same as <c>Platform.Routes.ResetPassword</c>.</summary>
    public const string ResetPath = "/account/reset-password";

    /// <inheritdoc />
    public Task SendInvitationAsync(string email, string organizationName, string token, CancellationToken cancellationToken = default) =>
        SendAsync(email, "Email_Invitation", options.Link(AcceptPath, token), organizationName, cancellationToken);

    /// <inheritdoc />
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default) =>
        SendAsync(email, "Email_PasswordReset", options.Link(ResetPath, token), "", cancellationToken);

    /// <summary>One message in both languages: English first, then German.</summary>
    public static (string Subject, string Body) Compose(string kind, Uri link, string organizationName)
    {
        string Text(string key, string culture) =>
            string.Format(CultureInfo.InvariantCulture, Texts.GetString(key, CultureInfo.GetCultureInfo(culture)) ?? key, organizationName, link);

        // Each key spelled out, so the resource tests can find it.
        var (subjectKey, bodyKey) = kind switch
        {
            "Email_Invitation" => ("Email_Invitation_Subject", "Email_Invitation_Body"),
            "Email_PasswordReset" => ("Email_PasswordReset_Subject", "Email_PasswordReset_Body"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "There is no such message."),
        };

        return (
            Text(subjectKey, "en") + " / " + Text(subjectKey, "de"),
            Text(bodyKey, "en") + "\n\n— — —\n\n" + Text(bodyKey, "de") + "\n");
    }

    private async Task SendAsync(string email, string kind, Uri link, string organizationName, CancellationToken cancellationToken)
    {
        var (subject, body) = Compose(kind, link, organizationName);

        using var message = new MailMessage(new MailAddress(options.From), new MailAddress(email))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.StartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = options.User is { Length: > 0 } user ? new NetworkCredential(user, options.Password) : null,
        };

        try
        {
            await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
            Sent(logger, kind);
        }
        catch (SmtpException exception)
        {
            // The address and the reason, never the message: it carries the token.
            NotSent(logger, kind, email, exception.StatusCode, exception);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent {Kind} by SMTP.")]
    private static partial void Sent(ILogger logger, string kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not send {Kind} to {Email}: {Status}.")]
    private static partial void NotSent(ILogger logger, string kind, string email, SmtpStatusCode status, Exception exception);
}
