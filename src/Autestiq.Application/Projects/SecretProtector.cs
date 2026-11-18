using Microsoft.EntityFrameworkCore;
using Autestiq.Persistence;

namespace Autestiq.Application.Projects;

/// <summary>
/// Encrypts and decrypts a test account's sign-in values (ADR-0033). The host provides it with ASP.NET Core Data
/// Protection and a key ring kept outside the database; the application layer never sees a key.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a value under a purpose, so it reads back only under the same purpose and key ring.</summary>
    string Protect(string purpose, string value);

    /// <summary>Decrypts a value, or <c>null</c> when it cannot be read: another purpose, or a key ring that is not the one it was written with.</summary>
    string? Unprotect(string purpose, string ciphertext);
}

/// <summary>
/// What runs when no host has provided a protector: nothing can be stored or read, rather than stored in the clear.
/// </summary>
internal sealed class NoSecretProtector : ISecretProtector
{
    public string Protect(string purpose, string value) =>
        throw new InvalidOperationException("No secret protector is configured, so no sign-in value can be stored.");

    public string? Unprotect(string purpose, string ciphertext) => null;
}

/// <summary>Where an environment's secrets are read and named, for the services that use them.</summary>
internal static class EnvironmentSecrets
{
    /// <summary>The purpose a value is encrypted under: one per organization and environment.</summary>
    public static string Purpose(Guid organizationId, Guid environmentId) =>
        $"Autestiq.EnvironmentSecret.v1/{organizationId:N}/{environmentId:N}";

    /// <summary>The names an environment has values for. Across the filter, held to the organization given.</summary>
    public static async Task<IReadOnlyList<string>> NamesAsync(AutestiqDbContext database, Guid organizationId, Guid environmentId, CancellationToken cancellationToken) =>
        await database.EnvironmentSecrets.IgnoreQueryFilters()
            .Where(secret => secret.OrganizationId == organizationId && secret.EnvironmentId == environmentId)
            .OrderBy(secret => secret.Name)
            .Select(secret => secret.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The decrypted values an environment has, by name, for a worker about to run, or <c>null</c> when any cannot be
    /// read (a lost or changed key ring): a run then is refused rather than run without them. Across the filter, held
    /// to the organization given, because the dispatcher has none in context.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>?> ValuesAsync(
        AutestiqDbContext database, ISecretProtector protector, Guid organizationId, Guid environmentId, CancellationToken cancellationToken)
    {
        var stored = await database.EnvironmentSecrets.IgnoreQueryFilters()
            .Where(secret => secret.OrganizationId == organizationId && secret.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var purpose = Purpose(organizationId, environmentId);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var secret in stored)
        {
            if (protector.Unprotect(purpose, secret.Ciphertext) is not { } value)
            {
                return null;
            }

            values[secret.Name] = value;
        }

        return values;
    }
}
