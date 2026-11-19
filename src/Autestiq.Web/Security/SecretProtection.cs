using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Autestiq.Application.Projects;

namespace Autestiq.Web.Security;

/// <summary>
/// The key ring and the protector for a test account's sign-in values (ADR-0033).
/// </summary>
/// <remarks>
/// <para>
/// The key ring is kept in a directory named by configuration, outside the database, so a copy of the database alone
/// reads none of the values. It moves to the cloud key service once hosting is chosen; nothing else changes then.
/// </para>
/// <para>
/// Outside development the application does not start without it: a key ring in a default place could be lost with
/// the machine, and every stored value with it, or sit where nobody looked after it.
/// </para>
/// </remarks>
internal static class SecretProtection
{
    /// <summary>Where the key ring is kept: a directory, outside the database.</summary>
    public const string KeyDirectoryKey = "DataProtection:KeyDirectory";

    /// <summary>Adds Data Protection with its key ring where configuration says, and the protector the application uses.</summary>
    public static void AddAutestiqSecretProtection(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var protection = builder.Services.AddDataProtection().SetApplicationName("Autestiq");

        if (builder.Configuration[KeyDirectoryKey] is { Length: > 0 } directory)
        {
            protection.PersistKeysToFileSystem(new DirectoryInfo(directory));
        }
        else if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{KeyDirectoryKey} is not configured. It names the directory, outside the database, where the keys that "
                + "encrypt test accounts' sign-in values are kept (ADR-0033). Set it, for example with the environment "
                + "variable DataProtection__KeyDirectory, to a directory that is backed up and readable only by this application.");
        }

        builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    }
}

/// <summary>A test account's values, encrypted with ASP.NET Core Data Protection under a purpose per organization and environment.</summary>
internal sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    /// <inheritdoc />
    public string Protect(string purpose, string value) => provider.CreateProtector(purpose).Protect(value);

    /// <inheritdoc />
    public string? Unprotect(string purpose, string ciphertext)
    {
        try
        {
            return provider.CreateProtector(purpose).Unprotect(ciphertext);
        }
        catch (CryptographicException)
        {
            // Another purpose, another key ring, or a value that was tampered with: read as nothing.
            return null;
        }
    }
}
