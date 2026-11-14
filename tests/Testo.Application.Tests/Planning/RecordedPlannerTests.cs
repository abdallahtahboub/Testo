using Testo.Ai;
using Testo.Contracts.Planning;

namespace Testo.Application.Tests.Planning;

/// <summary>
/// The planner that replays a stored answer.
/// </summary>
/// <remarks>
/// It had no test, and its own documentation claimed that CI replays real recorded answers through
/// it — which was not true: nothing is committed to replay and CI runs only the self-check. The
/// claim is corrected and the class is now actually covered, which is the order those two things
/// should have happened in.
/// </remarks>
public class RecordedPlannerTests
{
    private static readonly SiteSnapshot Shop = SiteSnapshot.Empty("http://127.0.0.1:5090");

    [Fact]
    public async Task PlanAsync_AnAnswerOnDisk_IsReplayedAtNoCost()
    {
        var directory = Directory.CreateTempSubdirectory("testo-recordings-");

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, RecordedPlanner.Key("Buy a teapot") + ".json"),
                """
                {
                  "scenarioName": "Buy a teapot",
                  "actions": [
                    { "kind": "goto", "description": "Open the catalogue", "path": "/" },
                    { "kind": "assertVisible", "description": "The teapot is listed", "target": { "kind": "testId", "value": "name-pot" } }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var result = await RecordedPlanner.FromDirectory(directory.FullName)
                .PlanAsync(Request("Buy a teapot"), TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.Plan!.Actions.Count);

            // Nothing was spent, and the table must say so rather than invent a price.
            Assert.Equal(0m, result.Cost.Cost);
            Assert.Equal(0, result.Cost.TotalTokens);
            Assert.Equal(1, result.Cost.Attempts);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PlanAsync_AScenarioNobodyRecorded_SaysThereIsNoPlanner()
    {
        var directory = Directory.CreateTempSubdirectory("testo-recordings-");

        try
        {
            var result = await RecordedPlanner.FromDirectory(directory.FullName)
                .PlanAsync(Request("Something nobody ran"), TestContext.Current.CancellationToken);

            // Not "unreadable": there was nothing to read. A replay with no recording is a missing
            // planner, and the report says which scenarios had none.
            Assert.False(result.Succeeded);
            Assert.Contains(PlanningErrors.NoPlanner, result.Errors);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void FromDirectory_ADirectoryThatIsNotThere_IsEmptyRatherThanAThrow()
    {
        var planner = RecordedPlanner.FromDirectory(Path.Combine(Path.GetTempPath(), "testo-no-such-dir"));

        Assert.Equal("recorded", planner.Model);
    }

    [Theory]
    [InlineData("Buy a teapot", "buy-a-teapot")]
    [InlineData("  Sign in with a wrong password!  ", "sign-in-with-a-wrong-password")]
    [InlineData("£53.25 total", "53-25-total")]
    [InlineData("!!!", "scenario")]
    public void Key_AScenariosWords_BecomeANameOnDisk(string scenario, string expected)
    {
        // A recording has to be recognizable on disk and a scenario has to find its own answer, so
        // the mapping is a function of the words and nothing else.
        Assert.Equal(expected, RecordedPlanner.Key(scenario));
    }

    private static PlanRequest Request(string scenario) =>
        PlanRequest.First(scenario, Shop, [], maxActions: 20, maxTokens: 4_000);
}
