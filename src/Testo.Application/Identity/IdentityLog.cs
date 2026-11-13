using Microsoft.Extensions.Logging;

namespace Testo.Application.Identity;

/// <summary>
/// The log messages of the identity use cases, generated rather than formatted at the call site,
/// so that nothing is built when the level is switched off.
/// </summary>
internal static partial class IdentityLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Sign-in refused for an address with no account, from {IpAddress}.")]
    public static partial void UnknownAddress(ILogger logger, string? ipAddress);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{UserId} signed in correctly but belongs to no active organization.")]
    public static partial void NoActiveMembership(ILogger logger, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No mail provider is configured. The invitation to {Organization} for {Email} would carry the token {Token}.")]
    public static partial void InvitationNotSent(ILogger logger, string organization, string email, string token);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No mail provider is configured. The password reset for {Email} would carry the token {Token}.")]
    public static partial void PasswordResetNotSent(ILogger logger, string email, string token);
}
