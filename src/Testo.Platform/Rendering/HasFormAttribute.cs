namespace Testo.Platform.Rendering;

/// <summary>
/// Marks a public page that contains a form which posts back to the page, such as a contact
/// form or the sign-in page.
/// </summary>
/// <remarks>
/// Only pages with this attribute get an antiforgery token. All other public pages stay
/// free of the antiforgery cookie and can be cached by the browser. A form on a page
/// without the attribute is rejected by the server with "400 Bad Request".
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class HasFormAttribute : Attribute;
