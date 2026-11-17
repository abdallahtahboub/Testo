using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Execution;

namespace Autestiq.Application.Tests.Execution;

/// <summary>
/// Proof that an organization controls an origin before any run is aimed at it (Milestone 3.5, gap G1).
/// </summary>
public sealed class OriginProofTests : IAsyncLifetime
{
    private const string Shop = "https://shop.example";
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private Guid _first;
    private Guid _second;
    private Guid _firstProject;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_first, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        (_second, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Contoso");
        _firstProject = await ExecutionSupport.ProjectAsync(_database, _first, "SHOP");
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Theory]
    [InlineData("https://shop.example", "https://shop.example:443")]
    [InlineData("HTTPS://Shop.Example/", "https://shop.example:443")]
    [InlineData("http://shop.example:8080", "http://shop.example:8080")]
    [InlineData("https://shop.example/checkout", null)]
    [InlineData("https://shop.example/?q=1", null)]
    [InlineData("https://ada:secret@shop.example", null)]
    [InlineData("ftp://shop.example", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("shop.example", null)]
    [InlineData("", null)]
    public void Normalize_KeepsOnlyAPlainOrigin(string origin, string? expected)
    {
        Assert.Equal(expected, Origins.Normalize(origin));
    }

    [Theory]
    [InlineData("https://shop.example:443", "https://shop.example")]
    [InlineData("https://shop.example:443/.well-known/autestiq-verification.txt", "https://shop.example/.well-known/autestiq-verification.txt")]
    [InlineData("http://shop.example:80", "http://shop.example")]
    [InlineData("http://shop.example:8080", "http://shop.example:8080")]
    [InlineData("https://shop.example:8443/x", "https://shop.example:8443/x")]
    public void Display_LeavesOutOnlyTheSchemesOwnPort(string address, string expected)
    {
        Assert.Equal(expected, Origins.Display(address));
    }

    [Fact]
    public async Task Request_IssuesAnUnguessableToken_AndTheSameOneWhenAskedAgain()
    {
        var first = await AsAsync(_first, Role.Owner, proofs => proofs.RequestAsync(Shop));
        var again = await AsAsync(_first, Role.Owner, proofs => proofs.RequestAsync("HTTPS://SHOP.EXAMPLE/"));

        Assert.True(first.Succeeded);
        Assert.Equal(43, first.Value!.Token.Length);
        Assert.Equal(first.Value.Token, again.Value!.Token);
        Assert.Equal("https://shop.example:443/.well-known/autestiq-verification.txt", first.Value.FileUrl);
    }

    [Fact]
    public async Task Request_ByASecondOrganization_ForTheSameOrigin_IsATokenOfItsOwn()
    {
        var first = await AsAsync(_first, Role.Owner, proofs => proofs.RequestAsync(Shop));
        var second = await AsAsync(_second, Role.Owner, proofs => proofs.RequestAsync(Shop));

        Assert.NotEqual(first.Value!.Token, second.Value!.Token);
    }

    [Fact]
    public async Task Request_SomebodyWhoCannotManageProjects_IsRefused()
    {
        var result = await AsAsync(_first, Role.QaEngineer, proofs => proofs.RequestAsync(Shop));

        Assert.Equal(["Execution_Error_NotAllowed"], result.Errors);
    }

    [Fact]
    public async Task Request_NotAnOrigin_IsRefused()
    {
        var result = await AsAsync(_first, Role.Owner, proofs => proofs.RequestAsync("https://shop.example/admin"));

        Assert.Equal(["Execution_Error_InvalidOrigin"], result.Errors);
    }

    [Fact]
    public async Task Check_QueuesTheProofForAWorker_AndNeverFetchesAnything()
    {
        await AsAsync(_first, Role.Owner, proofs => proofs.RequestAsync(Shop));
        var checking = await AsAsync(_first, Role.Owner, proofs => proofs.CheckAsync(Shop));
        var state = await AsAsync(_first, Role.Owner, proofs => proofs.GetAsync(Shop));

        Assert.True(checking.Succeeded);
        Assert.Equal(OriginProofStatus.Checking, state!.Status);
    }

    [Fact]
    public async Task MayTarget_OurOwnConfiguredOrigin_NeedsNoProof()
    {
        Assert.True(await MayTargetAsync("http://127.0.0.1:5090"));
    }

    [Fact]
    public async Task MayTarget_AnOriginNobodyProved_IsRefused()
    {
        Assert.False(await MayTargetAsync(Shop));
    }

    [Fact]
    public async Task MayTarget_AnotherOrganizationsProof_CountsForNothing()
    {
        await ProofAsync(_second, verifiedUntil: Now + TimeSpan.FromDays(10));

        Assert.False(await MayTargetAsync(Shop));
    }

    [Fact]
    public async Task MayTarget_AnExpiredProof_IsRefused()
    {
        await ProofAsync(_first, verifiedUntil: Now - TimeSpan.FromMinutes(1));

        Assert.False(await MayTargetAsync(Shop));
    }

    [Fact]
    public async Task MayTarget_TheOrganizationsOwnVerifiedProof_IsAllowed_FromASealedContainer()
    {
        await ProofAsync(_first, verifiedUntil: Now + TimeSpan.FromDays(10));

        Assert.True(await MayTargetAsync("HTTPS://shop.example/", WorkerIsolation.Container));
    }

    [Fact]
    public async Task MayTarget_AProvedSiteListedAsProduction_IsRefused_UntilAnOwnerAuthorizesIt()
    {
        // ADR-0030: production is off by default, whatever the proof says.
        await ProofAsync(_first, verifiedUntil: Now + TimeSpan.FromDays(10));
        var environment = await EnvironmentAsync(Domain_Production(authorizedAt: null));

        Assert.False(await MayTargetAsync(Shop, WorkerIsolation.Container));

        await using (var database = _database.NewContext())
        {
            var stored = await database.Environments.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == environment, TestContext.Current.CancellationToken);
            stored.ProductionAuthorizedAt = Now;

            using (_database.WriteScope.AllowWritingTo(_first))
            {
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        Assert.True(await MayTargetAsync(Shop, WorkerIsolation.Container));
    }

    [Fact]
    public async Task MayTarget_AnotherOrganizationsUnauthorizedProductionEntry_DoesNotBlockThisOne()
    {
        await ProofAsync(_first, verifiedUntil: Now + TimeSpan.FromDays(10));
        var secondProject = await ExecutionSupport.ProjectAsync(_database, _second, "LIVE");
        await EnvironmentAsync(Domain_Production(authorizedAt: null), _second, secondProject);

        Assert.True(await MayTargetAsync(Shop, WorkerIsolation.Container));
    }

    private static (Contracts.Projects.EnvironmentKind Kind, DateTimeOffset? AuthorizedAt) Domain_Production(DateTimeOffset? authorizedAt) =>
        (Contracts.Projects.EnvironmentKind.Production, authorizedAt);

    private async Task<Guid> EnvironmentAsync((Contracts.Projects.EnvironmentKind Kind, DateTimeOffset? AuthorizedAt) kind, Guid? organization = null, Guid? project = null)
    {
        await using var database = _database.NewContext();
        var environment = new Domain.Projects.TestEnvironment
        {
            OrganizationId = organization ?? _first,
            ProjectId = project ?? _firstProject,
            Name = "Live",
            Origin = "https://shop.example:443",
            Kind = kind.Kind,
            ProductionAuthorizedAt = kind.AuthorizedAt,
        };
        database.Environments.Add(environment);

        using (_database.WriteScope.AllowWritingTo(environment.OrganizationId))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return environment.Id;
    }

    [Fact]
    public async Task MayTarget_AVerifiedProof_IsNotEnough_WhenWorkersRunAsProcesses()
    {
        // ADR-0026: a customer's origin is reached only from a sealed container, whatever the proof says.
        await ProofAsync(_first, verifiedUntil: Now + TimeSpan.FromDays(10));

        Assert.False(await MayTargetAsync(Shop, WorkerIsolation.Process));
    }

    [Fact]
    public void ProofJob_NamesTheOriginAsTheOnlyOneAllowed_AndTrustsNothingLocal()
    {
        var proof = new OriginProof { Origin = "https://shop.example:443", Token = "token" };

        var job = RunDispatcher.ProofJobFor(proof, "/tmp/check");

        Assert.Equal(WorkerTask.VerifyOrigin, job.Task);
        Assert.Equal(["https://shop.example:443"], job.AllowedOrigins);
        Assert.Empty(job.TrustedLocalOrigins);
        Assert.Equal("token", job.OriginProof);
    }

    [Theory]
    [InlineData(RunStatus.Passed, OriginProofStatus.Verified)]
    [InlineData(RunStatus.Failed, OriginProofStatus.Failed)]
    [InlineData(RunStatus.InfrastructureError, OriginProofStatus.Failed)]
    public void ApplyProofResult_PassedVerifiesForAWhile_AnythingElseFails(RunStatus outcome, OriginProofStatus expected)
    {
        var proof = new OriginProof { Origin = Shop, Token = "token", Status = OriginProofStatus.Checking };
        var result = new WorkerResult(proof.Id, outcome, outcome == RunStatus.Passed ? null : "Not there.", Now, Now, [], 0);

        RunDispatcher.ApplyProofResult(proof, result, Now, TimeSpan.FromDays(30));

        Assert.Equal(expected, proof.Status);
        Assert.Equal(expected == OriginProofStatus.Verified ? Now + TimeSpan.FromDays(30) : null, proof.ExpiresAt);
    }

    [Fact]
    public void ApplyProofResult_AnAnswerAboutAnotherCheck_IsAFailure()
    {
        var proof = new OriginProof { Origin = Shop, Token = "token", Status = OriginProofStatus.Checking };
        var result = new WorkerResult(Guid.CreateVersion7(), RunStatus.Passed, null, Now, Now, [], 0);

        RunDispatcher.ApplyProofResult(proof, result, Now, TimeSpan.FromDays(30));

        Assert.Equal(OriginProofStatus.Failed, proof.Status);
    }

    [Fact]
    public async Task ARunAimedAtAnUnprovedOrigin_IsRefusedByTheDispatcher_AndNoWorkerIsStarted()
    {
        // The worker writes a file when it starts, so a refusal can be told from a run that started and failed.
        var marker = Path.Combine(Directory.CreateTempSubdirectory("autestiq-marker-").FullName, "started");
        var options = new ChangeableOptions(new ExecutionOptions { WorkerPath = MarkingWorker(marker), MaxConcurrentRuns = 1 });

        await using var services = _database.NewServices();
        var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);

        var runId = await ExecutionSupport.RunAsync(_database, _first, _firstProject, run => run.TargetOrigin = Shop);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(RunStatus.Cancelled, await ExecutionSupport.StatusOnceItIsAsync(_database, runId, RunStatus.Cancelled, TimeSpan.FromSeconds(20)));
            Assert.StartsWith("Refused", (await ExecutionSupport.StoredAsync(_database, runId)).FailureReason, StringComparison.Ordinal);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    private async Task<T> AsAsync<T>(Guid organization, Role role, Func<IOriginProofService, Task<T>> act)
    {
        await using var services = _database.NewServices();
        _database.Principal.Become(organization, Guid.CreateVersion7(), role);

        using var scope = services.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<IOriginProofService>());
    }

    private async Task<bool> MayTargetAsync(string target, WorkerIsolation isolation = WorkerIsolation.Container)
    {
        await using var database = _database.NewContext();
        var run = new TestRun { OrganizationId = _first, ScenarioId = "basket", TargetOrigin = target };

        return await RunDispatcher.MayTargetAsync(
            database, run, new ExecutionOptions { TargetOrigin = "http://127.0.0.1:5090", Isolation = isolation }, Now, TestContext.Current.CancellationToken);
    }

    private async Task ProofAsync(Guid organization, DateTimeOffset verifiedUntil)
    {
        await using var database = _database.NewContext();

        database.OriginProofs.Add(new OriginProof
        {
            OrganizationId = organization,
            Origin = Origins.Normalize(Shop)!, // A constant that is an origin; Normalize_KeepsOnlyAPlainOrigin says so.
            Token = "token",
            Status = OriginProofStatus.Verified,
            IssuedAt = Now - TimeSpan.FromDays(40),
            VerifiedAt = verifiedUntil - TimeSpan.FromDays(30),
            ExpiresAt = verifiedUntil,
        });

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A worker that leaves a mark when it starts, then waits.</summary>
    private static string MarkingWorker(string marker)
    {
        var directory = Directory.CreateTempSubdirectory("autestiq-marking-worker-").FullName;

        if (OperatingSystem.IsWindows())
        {
            var batch = Path.Combine(directory, "worker.cmd");
            File.WriteAllText(batch, $"@echo started > \"{marker}\"\r\n@ping -n 60 127.0.0.1 > nul\r\n");
            return batch;
        }

        var script = Path.Combine(directory, "worker.sh");
        File.WriteAllText(script, $"#!/bin/sh\ntouch '{marker}'\nsleep 60\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }
}
