namespace Testo.Web.Identity;

/// <summary>
/// The claims the session cookie carries, beyond the ones .NET already names.
/// </summary>
/// <remarks>
/// Only what every request needs in order to decide what a person may see, so that the ordinary
/// case costs no database read. What must be fresh — whether the membership still exists — is
/// looked up each time instead, by <c>SessionValidator</c>.
/// </remarks>
internal static class TestoClaims
{
    /// <summary>The organization this session is acting in.</summary>
    public const string Organization = "testo:org";

    /// <summary>Its name, so the top bar needs no query to show it.</summary>
    public const string OrganizationName = "testo:org_name";

    /// <summary>The person's role in that organization.</summary>
    public const string Role = "testo:role";

    /// <summary>
    /// What the account's security stamp was when the session began. A password change moves the
    /// stamp, and every session issued before it stops matching.
    /// </summary>
    public const string SecurityStamp = "testo:stamp";
}
