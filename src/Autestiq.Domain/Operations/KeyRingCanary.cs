using Autestiq.Domain.Common;

namespace Autestiq.Domain.Operations;

/// <summary>
/// A known value encrypted with the key ring, kept in the database (R-20). A key ring that cannot read it is not the
/// one the database's test-account values were written with, so the application is not ready. One row, product-wide.
/// </summary>
public sealed class KeyRingCanary : Entity
{
    /// <summary>The one row's id.</summary>
    public static readonly Guid OnlyId = new("01a1e500-0000-7000-8000-00000000c0de");

    /// <summary>The purpose the canary is encrypted under, apart from every customer value's.</summary>
    public const string Purpose = "Autestiq.KeyRingCanary.v1";

    /// <summary>What the canary decrypts to.</summary>
    public const string Plaintext = "the key ring that wrote this database";

    /// <summary>The canary, encrypted.</summary>
    public required string Ciphertext { get; set; }
}
