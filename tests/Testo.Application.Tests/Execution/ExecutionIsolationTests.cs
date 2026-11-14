using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testo.Application.Execution;
using Testo.Application.Tests.Support;
using Testo.Contracts.Execution;
using Testo.Contracts.Identity;
using Testo.Domain.Execution;
using Testo.Domain.Projects;

namespace Testo.Application.Tests.Execution;

/// <summary>
/// The refusals the threat model asks of the application side: one organization's runs and
/// evidence are not another's (<c>docs/execution-threat-model.md</c> §7).
/// </summary>
public sealed class ExecutionIsolationTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;
    private Guid _first;
    private Guid _second;
    private Guid _firstProject;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _services = _database.NewServices();

        (_first, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Northwind");
        _firstProject = await ProjectAsync(_first, "SHOP");

        (_second, _) = await Seed.OrganizationWithOwnerAsync(_database, _services, "Contoso");
        await ProjectAsync(_second, "OTHER");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task ARunOfAnotherOrganization_DoesNotExist()
    {
        var runId = await RunAsync(_first, _firstProject);

        // Not refused — not there, which is the same answer as for a run that never existed.
        // Nothing leaks, not even that it is real.
        _database.Principal.Become(_second, Guid.CreateVersion7(), Role.Owner);

        using var scope = _services.CreateScope();
        var execution = scope.ServiceProvider.GetRequiredService<IExecutionService>();

        Assert.Null(await execution.GetRunAsync(runId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARunOfTheOwnOrganization_IsFound()
    {
        // The same question for the organization it belongs to, so that the test above is proving
        // isolation rather than that nothing was written.
        var runId = await RunAsync(_first, _firstProject);

        _database.Principal.Become(_first, Guid.CreateVersion7(), Role.Owner);

        using var scope = _services.CreateScope();
        var execution = scope.ServiceProvider.GetRequiredService<IExecutionService>();

        Assert.NotNull(await execution.GetRunAsync(runId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnArtifactOfAnotherOrganization_DoesNotExist()
    {
        var runId = await RunAsync(_first, _firstProject);
        var artifactId = await ScreenshotAsync(_first, runId);

        // This is the question the artifact endpoint asks, through the same filter.
        _database.Principal.OrganizationId = _second;

        await using var asSecond = _database.NewContext();

        Assert.Null(await asSecond.Artifacts
            .FirstOrDefaultAsync(artifact => artifact.Id == artifactId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnArtifactOfTheOwnOrganization_IsFound()
    {
        var runId = await RunAsync(_first, _firstProject);
        var artifactId = await ScreenshotAsync(_first, runId);

        _database.Principal.OrganizationId = _first;

        await using var asFirst = _database.NewContext();

        Assert.NotNull(await asFirst.Artifacts
            .FirstOrDefaultAsync(artifact => artifact.Id == artifactId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AStepWrittenIntoAnotherOrganizationsRun_IsRefused()
    {
        // The third layer. The foreign key is (organization_id, run_id), so a step of one
        // organization cannot name a run of another even if every check above it is wrong.
        var runId = await RunAsync(_first, _firstProject);

        _database.Principal.OrganizationId = _second;

        await using var database = _database.NewContext();

        database.TestRunSteps.Add(new TestRunStep
        {
            OrganizationId = _second,
            TestRunId = runId,
            Order = 1,
            Description = "Planted",
            Status = StepStatus.Passed,
        });

        await Assert.ThrowsAnyAsync<Exception>(
            () => database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheSignatureOnAnArtifactLink_IsRequiredAndBoundToItsArtifact()
    {
        var links = _services.GetRequiredService<ArtifactLinks>();
        var artifactId = Guid.CreateVersion7();

        var token = links.Sign(artifactId);

        Assert.True(links.IsValid(artifactId, token));

        // Not for another artifact, not absent, not tampered with, not invented.
        Assert.False(links.IsValid(Guid.CreateVersion7(), token));
        Assert.False(links.IsValid(artifactId, null));
        Assert.False(links.IsValid(artifactId, token + "x"));
        Assert.False(links.IsValid(artifactId, "9999999999.not-a-signature"));
    }

    private async Task<Guid> RunAsync(Guid organizationId, Guid projectId)
    {
        await using var database = _database.NewContext();

        var run = new TestRun
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Number = Random.Shared.Next(1, 100_000),
            ScenarioId = Scenarios.DemoShopBasket,
            TargetOrigin = "http://127.0.0.1:5090",
            QueuedAt = DateTimeOffset.UtcNow,
        };

        database.TestRuns.Add(run);

        using (_database.WriteScope.AllowWritingTo(organizationId))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return run.Id;
    }

    private async Task<Guid> ScreenshotAsync(Guid organizationId, Guid runId)
    {
        await using var database = _database.NewContext();

        var artifact = new Artifact
        {
            OrganizationId = organizationId,
            TestRunId = runId,
            Kind = ArtifactKind.Screenshot,
            StorageKey = $"{organizationId:N}/{runId:N}/step-001.png",
            ContentType = "image/png",
            SizeBytes = 1024,
            CapturedAt = DateTimeOffset.UtcNow,
        };

        database.Artifacts.Add(artifact);

        using (_database.WriteScope.AllowWritingTo(organizationId))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return artifact.Id;
    }

    private async Task<Guid> ProjectAsync(Guid organizationId, string prefix)
    {
        await using var database = _database.NewContext();

        var project = new Project { OrganizationId = organizationId, Name = prefix, KeyPrefix = prefix };
        database.Projects.Add(project);

        using (_database.WriteScope.AllowWritingTo(organizationId))
        {
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return project.Id;
    }
}
