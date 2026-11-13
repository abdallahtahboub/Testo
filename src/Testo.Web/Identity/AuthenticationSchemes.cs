namespace Testo.Web.Identity;

/// <summary>The two cookies a sign-in can involve.</summary>
internal static class AuthenticationSchemes
{
    /// <summary>The session proper, issued once somebody is fully signed in.</summary>
    public const string Session = "Testo.Session";

    /// <summary>
    /// The pause between a correct password and a correct one-time code. Separate from the
    /// session so that a half-finished sign-in cannot reach anything.
    /// </summary>
    public const string TwoFactor = "Testo.TwoFactor";
}
