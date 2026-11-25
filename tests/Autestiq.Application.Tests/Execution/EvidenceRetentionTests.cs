using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Autestiq.Application.Execution;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Contracts.Projects;
using Autestiq.Domain.Execution;

namespace Autestiq.Application.Tests.Execution;

/// <summary>
/// Evidence kept by policy and deleted when its time is up (gap analysis G-5, commercial-readiness G12): which
/// screenshots go and which stay, that the run and its steps stay, that deletion is audited, and that a project that
/// keeps none never has one stored.
/// </summary>
public sealed class EvidenceRetentionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly string _artifacts = Directory.CreateTempSubdirectory("autestiq-retention-").FullName;
    private TestDatabase _database = null!;
    private Guid _northwind;
    private Guid _contoso;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();

        await using var services = _database.NewServices();
        (_northwind, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Northwind");
        (_contoso, _) = await Seed.OrganizationWithOwnerAsync(_database, services, "Contoso");
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        Directory.Delete(_artifacts, recursive: true);
    }

    private ServiceProvider Services() =>
        _database.NewServices(services => services.Configure<ArtifactStoreOptions>(options => options.Directory = _artifacts));

    /// <summary>A project keeping screenshots for so many days.</summary>
    private async Task<Guid> ProjectAsync(Guid organization, int days)
    {
        var project = await ExecutionSupport.ProjectAsync(_database, organization, $"P{Random.Shared.Next(1000, 9999)}");

        await using var database = _database.NewContext();
        var stored = await database.Projects.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == project, TestContext.Current.CancellationToken);
        stored.EvidenceRetentionDays = days;

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return project;
    }

    /// <summary>A finished run with one step and its screenshot, captured so many days before now, stored as a run stores it.</summary>
    private async Task<(Guid Run, Guid Artifact, string Key)> RunWithScreenshotAsync(Guid organization, Guid project, int daysAgo)
    {
        var run = await ExecutionSupport.RunAsync(_database, organization, project, shaped => shaped.Status = RunStatus.Passed);

        await using var services = Services();
        using var content = new MemoryStream([1, 2, 3]);
        var key = await services.GetRequiredService<IArtifactStore>().SaveAsync(organization, run, "step-1.png", content, TestContext.Current.CancellationToken);

        await using var database = _database.NewContext();
        var artifact = new Artifact
        {
            OrganizationId = organization,
            TestRunId = run,
            Kind = ArtifactKind.Screenshot,
            StorageKey = key,
            ContentType = "image/png",
            SizeBytes = 3,
            CapturedAt = Now.AddDays(-daysAgo),
        };
        database.Artifacts.Add(artifact);
        database.TestRunSteps.Add(new TestRunStep { OrganizationId = organization, TestRunId = run, Order = 1, Description = "Open", Status = StepStatus.Passed, ScreenshotArtifactId = artifact.Id });

        using (_database.WriteScope.AllowWritingTo(organization))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return (run, artifact.Id, key);
    }

    private async Task<int> SweepAsync()
    {
        await using var services = Services();
        var sweep = new EvidenceRetentionService(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedOptions<EvidenceRetentionOptions>(new EvidenceRetentionOptions()),
            new FixedTime(Now),
            NullLogger<EvidenceRetentionService>.Instance);

        return await sweep.SweepAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> StoredAsync(Guid artifact)
    {
        await using var database = _database.NewContext();
        return await database.Artifacts.IgnoreQueryFilters().AnyAsync(candidate => candidate.Id == artifact, TestContext.Current.CancellationToken);
    }

    private async Task<bool> OnDiskAsync(string key)
    {
        await using var services = Services();
        await using var file = await services.GetRequiredService<IArtifactStore>().OpenAsync(key, TestContext.Current.CancellationToken);
        return file is not null;
    }

    [Fact]
    public async Task Sweep_DeletesTheScreenshotsWhoseTimeIsUp_FilesAndRows_AndKeepsTheRunAndItsSteps_AndRecordsIt()
    {
        var project = await ProjectAsync(_northwind, 90);
        var (run, artifact, key) = await RunWithScreenshotAsync(_northwind, project, daysAgo: 91);

        Assert.Equal(1, await SweepAsync());

        Assert.False(await StoredAsync(artifact));
        Assert.False(await OnDiskAsync(key));

        await using var database = _database.NewContext();
        Assert.Equal(RunStatus.Passed, (await ExecutionSupport.StoredAsync(_database, run)).Status);
        var step = await database.TestRunSteps.IgnoreQueryFilters().SingleAsync(candidate => candidate.TestRunId == run, TestContext.Current.CancellationToken);
        Assert.Null(step.ScreenshotArtifactId);

        var audit = await database.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(entry => entry.Action == "evidence.expired" && entry.SubjectId == project, TestContext.Current.CancellationToken);
        Assert.Equal(_northwind, audit.OrganizationId);
        Assert.Contains("\"Screenshots\": 1", audit.After, StringComparison.Ordinal);
        Assert.Contains("\"Days\": 90", audit.After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sweep_LeavesWhatIsStillWithinItsProjectsTime_InEveryOrganization()
    {
        var keepsLong = await ProjectAsync(_northwind, 90);
        var keepsAWeek = await ProjectAsync(_northwind, 7);
        var contosos = await ProjectAsync(_contoso, 30);

        var young = await RunWithScreenshotAsync(_northwind, keepsLong, daysAgo: 10);
        var weekOld = await RunWithScreenshotAsync(_northwind, keepsAWeek, daysAgo: 10);
        var contosoYoung = await RunWithScreenshotAsync(_contoso, contosos, daysAgo: 10);

        Assert.Equal(1, await SweepAsync());

        Assert.True(await StoredAsync(young.Artifact));
        Assert.True(await OnDiskAsync(young.Key));
        Assert.False(await StoredAsync(weekOld.Artifact));
        Assert.True(await StoredAsync(contosoYoung.Artifact));
        Assert.True(await OnDiskAsync(contosoYoung.Key));
    }

    [Fact]
    public async Task Sweep_OfAProjectThatKeepsNone_DeletesItsScreenshotsAtOnce_AndASecondSweepFindsNothing()
    {
        var project = await ProjectAsync(_northwind, 0);
        var (_, artifact, _) = await RunWithScreenshotAsync(_northwind, project, daysAgo: 0);

        Assert.Equal(1, await SweepAsync());
        Assert.False(await StoredAsync(artifact));
        Assert.Equal(0, await SweepAsync());
    }

    [Fact]
    public async Task ARun_OfAProjectThatKeepsNone_HasNoScreenshotStored_WhileOneThatKeepsThemDoes()
    {
        var keeps = await ProjectAsync(_northwind, 90);
        var keepsNone = await ProjectAsync(_northwind, 0);
        var kept = await ExecutionSupport.RunAsync(_database, _northwind, keeps);
        var notKept = await ExecutionSupport.RunAsync(_database, _northwind, keepsNone);

        await using (var services = Services())
        {
            var options = new ChangeableOptions(new ExecutionOptions { WorkerPath = ExecutionSupport.ScreenshotWorker(), MaxConcurrentRuns = 1 });
            var dispatcher = new RunDispatcher(services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System, NullLogger<RunDispatcher>.Instance);
            await dispatcher.StartAsync(TestContext.Current.CancellationToken);

            try
            {
                Assert.Equal(RunStatus.Passed, await ExecutionSupport.StatusOnceItIsAsync(_database, kept, RunStatus.Passed, TimeSpan.FromSeconds(30)));
                Assert.Equal(RunStatus.Passed, await ExecutionSupport.StatusOnceItIsAsync(_database, notKept, RunStatus.Passed, TimeSpan.FromSeconds(30)));
            }
            finally
            {
                await dispatcher.StopAsync(CancellationToken.None);
            }
        }

        await using var database = _database.NewContext();
        Assert.True(await database.Artifacts.IgnoreQueryFilters().AnyAsync(artifact => artifact.TestRunId == kept, TestContext.Current.CancellationToken));
        Assert.False(await database.Artifacts.IgnoreQueryFilters().AnyAsync(artifact => artifact.TestRunId == notKept, TestContext.Current.CancellationToken));

        // The step and its result are kept either way.
        Assert.True(await database.TestRunSteps.IgnoreQueryFilters().AnyAsync(step => step.TestRunId == notKept && step.ScreenshotArtifactId == null, TestContext.Current.CancellationToken));
    }

    // --- Choosing the policy ---

    private async Task<Contracts.Common.OperationResult> SetAsync(Guid organization, Role role, Guid project, int days)
    {
        await using var services = Services();
        _database.Principal.Become(organization, Guid.CreateVersion7(), role);
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectService>().SetEvidenceRetentionAsync(project, days, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetEvidenceRetention_ByAQaLead_ToADayCountOffered_IsKept_AndAudited()
    {
        var project = await ProjectAsync(_northwind, 90);

        Assert.True((await SetAsync(_northwind, Role.QaLead, project, 7)).Succeeded);

        await using var database = _database.NewContext();
        Assert.Equal(7, (await database.Projects.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == project, TestContext.Current.CancellationToken)).EvidenceRetentionDays);
        var audit = await database.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(entry => entry.Action == "project.evidence_retention_changed" && entry.SubjectId == project, TestContext.Current.CancellationToken);
        Assert.Contains("90", audit.Before, StringComparison.Ordinal);
        Assert.Contains("7", audit.After, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Role.QaEngineer)]
    [InlineData(Role.Developer)]
    [InlineData(Role.Viewer)]
    public async Task SetEvidenceRetention_BySomebodyWhoCannotManageProjects_IsRefused(Role role)
    {
        var project = await ProjectAsync(_northwind, 90);

        Assert.Equal(["Projects_Error_NotAllowed"], (await SetAsync(_northwind, role, project, 7)).Errors);
    }

    [Theory]
    [InlineData(365)]
    [InlineData(91)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task SetEvidenceRetention_ToADayCountNotOffered_IsRefused_SoNothingIsKeptLongerThanNinetyDays(int days)
    {
        var project = await ProjectAsync(_northwind, 90);

        Assert.Equal(["Projects_Error_InvalidRetention"], (await SetAsync(_northwind, Role.Owner, project, days)).Errors);
    }

    [Fact]
    public async Task SetEvidenceRetention_OfAnotherOrganizationsProject_IsAsIfItDidNotExist()
    {
        var project = await ProjectAsync(_contoso, 90);

        Assert.Equal(["Projects_Error_NoSuchProject"], (await SetAsync(_northwind, Role.Owner, project, 0)).Errors);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
