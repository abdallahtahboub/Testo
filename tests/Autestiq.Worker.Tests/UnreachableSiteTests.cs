using System.Net;
using System.Net.Sockets;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Planning;
using Autestiq.Worker.Tests.Support;

namespace Autestiq.Worker.Tests;

/// <summary>
/// A site that cannot be reached is not a failure of it (R3 rehearsal F5; the owner, 2026-11-25): the run ends with no
/// verdict and says the site could not be reached. A site that is reached and wrong still fails.
/// </summary>
[Collection(Serially.Name)]
public class UnreachableSiteTests(DemoShopFixture shop)
{
    private static readonly PlannedAction[] OpenTheCatalogue =
    [
        new(ActionKind.Goto, "Open the catalogue", Path: "/"),
        new(ActionKind.AssertText, "The catalogue is the catalogue", ActionTarget.ByRole("heading", "Catalogue"), Text: "Catalogue"),
    ];

    [Fact]
    public async Task ASiteTheEgressPolicyRefuses_IsInconclusive_SayingItCouldNotBeReached()
    {
        // Our own shop, but not trusted by this job, as a private address of a customer's never is: the proxy refuses it.
        var result = await Jobs.PlannedUntrustedAsync(shop.Origin, OpenTheCatalogue);

        Assert.Equal(RunStatus.Inconclusive, result.Status);
        Assert.Contains("could not be reached", result.FailureReason!, StringComparison.Ordinal);
        Assert.Contains("not a failure of it", result.FailureReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASiteWhereNothingAnswers_IsInconclusive_SayingItCouldNotBeReached()
    {
        // A port that was free a moment ago: nothing listens there.
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var port = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();

        var result = await Jobs.PlannedAsync($"http://127.0.0.1:{port}", OpenTheCatalogue);

        Assert.Equal(RunStatus.Inconclusive, result.Status);
        Assert.Contains("could not be reached", result.FailureReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASiteThatIsReachedAndWrong_StillFails()
    {
        PlannedAction[] wrong =
        [
            new(ActionKind.Goto, "Open the catalogue", Path: "/"),
            new(ActionKind.AssertText, "A heading the shop does not have", ActionTarget.ByRole("heading", "Catalogue"), Text: "Checkout"),
        ];

        var result = await Jobs.PlannedAsync(shop.Origin, wrong);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.DoesNotContain("could not be reached", result.FailureReason!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://shop.example", "https://shop.example:443 — refused: not an allowed origin", null, true)]
    [InlineData("http://127.0.0.1:5090", "http://127.0.0.1:5090 — refused: a private address", null, true)]
    [InlineData("https://shop.example", "https://fonts.example:443 — refused: not an allowed origin", null, false)]
    [InlineData("https://shop.example", null, "page.goto: net::ERR_NAME_NOT_RESOLVED at https://shop.example/", true)]
    [InlineData("https://shop.example", null, "Locator expected to have text 'Checkout'", false)]
    public void Of_CountsOnlyTheRunsOwnOrigin_AndErrorsOfNoConnection(string target, string? refusal, string? failure, bool unreachable)
    {
        Assert.Equal(unreachable, Unreachable.Of(target, refusal is null ? [] : [refusal], failure) is not null);
    }
}
