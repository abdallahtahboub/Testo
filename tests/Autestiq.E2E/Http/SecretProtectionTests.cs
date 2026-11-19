using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Autestiq.Application.Projects;
using Autestiq.E2E.Support;

namespace Autestiq.E2E.Http;

/// <summary>
/// The real protector for a test account's sign-in values, as the running application has it (ADR-0033): the value is
/// not in what is stored, reads back only under its own purpose, and not at all with another key ring.
/// </summary>
public class SecretProtectionTests(AppFixture app) : HttpTest(app)
{
    private const string Password = "Pa ss w0rd!";

    [Fact]
    public void AValue_IsNotInItsCiphertext_AndReadsBackOnlyUnderItsOwnPurpose()
    {
        var protector = App.Services.GetRequiredService<ISecretProtector>();
        var purpose = EnvironmentSecrets.Purpose(Guid.CreateVersion7(), Guid.CreateVersion7());

        var ciphertext = protector.Protect(purpose, Password);

        Assert.DoesNotContain(Password, ciphertext, StringComparison.Ordinal);
        Assert.Equal(Password, protector.Unprotect(purpose, ciphertext));
        Assert.Null(protector.Unprotect(EnvironmentSecrets.Purpose(Guid.CreateVersion7(), Guid.CreateVersion7()), ciphertext));
    }

    [Fact]
    public void AValue_CannotBeReadWithAnotherKeyRing()
    {
        var ciphertext = App.Services.GetRequiredService<ISecretProtector>().Protect("purpose", Password);

        // A key ring of its own, as a copy of the database alone would meet.
        var elsewhere = DataProtectionProvider.Create(Directory.CreateTempSubdirectory("autestiq-other-keys-"), options => options.SetApplicationName("Autestiq"));

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => elsewhere.CreateProtector("purpose").Unprotect(ciphertext));
    }
}
