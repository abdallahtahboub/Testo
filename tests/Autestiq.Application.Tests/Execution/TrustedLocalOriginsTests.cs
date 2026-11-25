using Autestiq.Application.Execution;
using Autestiq.Domain.Execution;

namespace Autestiq.Application.Tests.Execution;

/// <summary>
/// R-11: the origins a worker may reach on its own machine come from configuration only, never from a stored run.
/// </summary>
public class TrustedLocalOriginsTests
{
    private static TestRun Run(string target) => new() { ScenarioId = "basket", TargetOrigin = target };

    [Fact]
    public void JobFor_ARunStoredWithALoopbackTargetConfigurationDoesNotName_DoesNotTrustIt()
    {
        // The regression R-11 asks for. Before the fix the run's own target was made trusted, so a customer who
        // wrote http://127.0.0.1:6379 into a run would have reached whatever listens on the worker's host.
        var options = new ExecutionOptions { TargetOrigin = "http://127.0.0.1:5090" };

        var job = RunDispatcher.JobFor(Run("http://127.0.0.1:6379"), options, "/tmp/run");

        Assert.Equal("http://127.0.0.1:6379", job.TargetOrigin);
        Assert.DoesNotContain("http://127.0.0.1:6379", job.TrustedLocalOrigins);
        Assert.Equal(["http://127.0.0.1:5090"], job.TrustedLocalOrigins);
    }

    [Fact]
    public void JobFor_TheConfiguredDemoTarget_IsStillTrusted()
    {
        var options = new ExecutionOptions { TargetOrigin = "http://127.0.0.1:5090" };

        var job = RunDispatcher.JobFor(Run("http://127.0.0.1:5090"), options, "/tmp/run");

        Assert.Contains("http://127.0.0.1:5090", job.TrustedLocalOrigins);
    }

    [Fact]
    public void JobFor_ConfiguredLocalOrigins_AreTheWholeList()
    {
        var options = new ExecutionOptions
        {
            TargetOrigin = "http://127.0.0.1:5090",
            LocalOrigins = ["http://127.0.0.1:5091", "http://127.0.0.1:5092"],
        };

        var job = RunDispatcher.JobFor(Run("http://127.0.0.1:5093"), options, "/tmp/run");

        Assert.Equal(["http://127.0.0.1:5091", "http://127.0.0.1:5092"], job.TrustedLocalOrigins);
    }
}
