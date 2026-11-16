using Microsoft.Playwright;
using Autestiq.Contracts.Execution;
using Autestiq.Worker.Scenarios;

namespace Autestiq.Worker;

/// <summary>
/// Whether an origin holds the proof an organization was given: its proof file, fetched through the egress proxy
/// like everything else the worker reaches, must contain exactly the token (Milestone 3.5).
/// </summary>
/// <remarks>
/// The proxy is what makes this safe to do with a URL a customer typed. A job for this task names the origin as
/// the only one allowed and none as trusted-local, so an origin that resolves to a private, loopback or metadata
/// address is refused before anything is fetched, and a redirect elsewhere is refused the same way.
/// </remarks>
internal static class OriginProofScenario
{
    /// <summary>The largest proof file read. A token is 43 characters; anything far longer is not a proof.</summary>
    private const int MaxLength = 1_024;

    public static Scenario For(string proof) => new(
        "verify-origin",
        "Check the origin's proof",
        "Fetches the proof file from the origin and compares it with the token the organization was given.",
        [new ScenarioStep("Fetch the proof file and compare it", (page, _) => CheckAsync(page, proof))]);

    private static async Task CheckAsync(IPage page, string proof)
    {
        IResponse? response;

        try
        {
            response = await page.GotoAsync(OriginProofFile.Path).ConfigureAwait(false);
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("ERR_HTTP_RESPONSE_CODE_FAILURE", StringComparison.Ordinal))
        {
            // What the browser says for an error answer with nothing in it, such as an empty 404.
            throw new PlaywrightException($"The origin did not serve its proof file at {OriginProofFile.Path}.", exception);
        }

        if (response is null)
        {
            throw new PlaywrightException("The origin gave no answer for its proof file.");
        }

        if (!response.Ok)
        {
            throw new PlaywrightException($"The origin did not serve its proof file at {OriginProofFile.Path}: it answered {response.Status}.");
        }

        var text = await response.TextAsync().ConfigureAwait(false);

        // What the file says is the customer's, and is never repeated in a message: only whether it matched.
        if (text.Length > MaxLength || !string.Equals(text.Trim(), proof, StringComparison.Ordinal))
        {
            throw new PlaywrightException("The origin's proof file does not hold the token this organization was given.");
        }
    }
}
