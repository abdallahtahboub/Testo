using System.Net;
using Autestiq.Ai;
using Autestiq.Contracts.Planning;

namespace Autestiq.Application.Tests.Planning;

/// <summary>
/// What a planner says when the provider will not plan. Each answer needs a different response from a
/// person, so each is told apart, and none of them reads as a fault in the application under test.
/// </summary>
public class AnthropicPlannerErrorTests
{
    private static async Task<PlanResult> AnswerAsync(HttpStatusCode status, string body = "")
    {
        using var handler = new FixedAnswer(status, body);
        using var client = new HttpClient(handler);
        var planner = new AnthropicPlanner(client, new PlannerOptions { ApiKey = "test-key", InputCostPerMillion = 2m, OutputCostPerMillion = 10m }, TimeProvider.System);

        return await planner.PlanAsync(
            PlanRequest.First("Check the basket", SiteSnapshot.Empty("http://127.0.0.1:5090"), [], maxActions: 10, maxTokens: 1000),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OutOfCredit_IsSaidAsOutOfCredit_NotAsUnreachable()
    {
        // The wording the provider uses, in the shape it sends it.
        var result = await AnswerAsync(
            HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"invalid_request_error","message":"Your credit balance is too low to access the API."}}""");

        Assert.Equal([PlanningErrors.ProviderOutOfCredit], result.Errors);
    }

    [Fact]
    public async Task AnyOtherBadRequest_IsNotMistakenForCredit()
    {
        var result = await AnswerAsync(HttpStatusCode.BadRequest, """{"type":"error","error":{"message":"max_tokens is too large"}}""");

        Assert.Equal([PlanningErrors.ProviderUnavailable], result.Errors);
    }

    [Fact]
    public async Task TooManyRequests_IsSaidAsRateLimited()
    {
        var result = await AnswerAsync(HttpStatusCode.TooManyRequests);

        Assert.Equal([PlanningErrors.ProviderRateLimited], result.Errors);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARefusedKey_IsStillSaidAsARefusedKey(HttpStatusCode status)
    {
        var result = await AnswerAsync(status);

        Assert.Equal([PlanningErrors.ProviderRefusedTheKey], result.Errors);
    }

    [Fact]
    public async Task AServerError_IsSaidAsUnavailable()
    {
        var result = await AnswerAsync(HttpStatusCode.InternalServerError);

        Assert.Equal([PlanningErrors.ProviderUnavailable], result.Errors);
    }

    [Fact]
    public async Task AKeyWithoutTheModelsPrices_IsNoPlanner_AndAsksNothing()
    {
        // Without prices the planning budget's cost limit cannot be kept (ADR-0029), so the planner must not spend.
        using var handler = new FixedAnswer(HttpStatusCode.OK, "");
        using var client = new HttpClient(handler);
        var planner = new AnthropicPlanner(client, new PlannerOptions { ApiKey = "test-key" }, TimeProvider.System);

        var result = await planner.PlanAsync(
            PlanRequest.First("Check the basket", SiteSnapshot.Empty("http://127.0.0.1:5090"), [], maxActions: 10, maxTokens: 1000),
            TestContext.Current.CancellationToken);

        Assert.Equal([PlanningErrors.NoPlanner], result.Errors);
        Assert.Equal(0, handler.Asked);
    }

    private sealed class FixedAnswer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
