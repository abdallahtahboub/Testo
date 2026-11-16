using Autestiq.Contracts.Execution;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>Checking that an origin holds an organization's proof (Milestone 3.5, gap G1).</summary>
[Collection(ProofSite)]
public class OriginProofTests(ProofSiteFixture site) : IClassFixture<ProofSiteFixture>
{
    private const string ProofSite = "proof site";
    private const string Token = "k3Vt7Qx2pL9mN4rS8wY1zB6cD0fG5hJ3aE7iO2uT9vX";

    [Fact]
    public async Task TheRightToken_Passes()
    {
        site.Proof = Token + "\n";

        var result = await Jobs.VerifyAsync(site.Origin, Token, trustLocal: true);

        Assert.Equal(RunStatus.Passed, result.Status);
    }

    [Fact]
    public async Task AnotherToken_Fails_WithoutRepeatingWhatTheFileSays()
    {
        site.Proof = "somebody-else's-token";

        var result = await Jobs.VerifyAsync(site.Origin, Token, trustLocal: true);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.DoesNotContain("somebody-else", result.FailureReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoProofFile_Fails()
    {
        site.Proof = null;

        var result = await Jobs.VerifyAsync(site.Origin, Token, trustLocal: true);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Contains("did not serve its proof file", result.FailureReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOriginOnLoopback_IsRefusedBeforeAnythingIsFetched_WhenTheJobDoesNotTrustIt()
    {
        // How every real check runs: the origin a customer typed is never trusted-local, so one that resolves
        // to this machine is refused by the proxy, and the proof it serves is never read.
        site.Proof = Token;

        var result = await Jobs.VerifyAsync(site.Origin, Token, trustLocal: false);

        Assert.NotEqual(RunStatus.Passed, result.Status);
    }
}
