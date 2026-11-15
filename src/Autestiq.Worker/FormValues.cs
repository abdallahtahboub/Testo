namespace Autestiq.Worker;

/// <summary>What a form is filled with during exploration: fixed, harmless values, never taken from the page.</summary>
/// <remarks>
/// The application decides nothing about what is typed. It can only be the reason one of these fixed
/// values is chosen over another: a field that asks for an email address gets the email value. None of
/// them is a credential, so a sign-in form is only ever seen refusing.
/// </remarks>
internal static class FormValues
{
    /// <summary>An address that is one, at a domain reserved for testing.</summary>
    public const string Email = "test@example.test";

    /// <summary>An address that is not one, to see what an application says about it.</summary>
    public const string NotAnEmail = "not-an-email";

    /// <summary>What is typed into a field, given what kind of field it is and how the form is being filled.</summary>
    /// <param name="fieldType">The field's <c>type</c> attribute, lower case; <c>text</c> when it has none.</param>
    /// <param name="isEmail">Whether the field asks for an email address, by its type or by its name.</param>
    /// <param name="mode">How the form is being filled. Never <see cref="FillMode.None"/>.</param>
    public static string For(string fieldType, bool isEmail, FillMode mode)
    {
        if (isEmail)
        {
            return mode == FillMode.WrongEmail ? NotAnEmail : Email;
        }

        return fieldType switch
        {
            "password" => "not-a-real-password",
            "number" => "1",
            "tel" => "0000 000000",
            "url" => "https://example.test",
            "date" => "2030-01-01",
            "time" => "09:00",
            _ => "Test value",
        };
    }
}

/// <summary>How a form is filled before a control is pressed.</summary>
internal enum FillMode
{
    /// <summary>Nothing is typed. The control is pressed with the page as it is.</summary>
    None,

    /// <summary>Every field gets a value of the kind it asks for.</summary>
    Valid,

    /// <summary>As <see cref="Valid"/>, except that an email field gets something that is not an address.</summary>
    WrongEmail,
}

/// <summary>What a control's form holds, which decides whether filling it is worth a visit.</summary>
internal enum FormKind
{
    /// <summary>Nothing to type into.</summary>
    None,

    /// <summary>Fields, none of them for an email address.</summary>
    Fields,

    /// <summary>Fields, at least one of them for an email address.</summary>
    FieldsWithEmail,
}

/// <summary>One press on a path: the control, and how its form was filled first.</summary>
internal sealed record PressStep(string TestId, FillMode Fill);
