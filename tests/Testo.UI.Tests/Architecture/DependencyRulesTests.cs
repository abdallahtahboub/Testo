using System.Reflection;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// Enforces the dependency rules of <c>docs/architecture.md</c> §4, so the module boundaries
/// cannot erode unnoticed.
/// </summary>
public class DependencyRulesTests
{
    private const string Contracts = "Testo.Contracts";
    private const string Domain = "Testo.Domain";
    private const string Persistence = "Testo.Persistence";
    private const string Application = "Testo.Application";
    private const string Worker = "Testo.Worker";
    private const string DemoShop = "Testo.DemoShop";
    private const string Platform = "Testo.Platform";
    private const string UiShared = "Testo.UI.Shared";
    private const string Host = "Testo.Web";

    /// <summary>Which projects a project may reference directly. "*" in a name matches any suffix.</summary>
    private static readonly (string Project, string[] MayReference)[] Rules =
    [
        (Contracts, []),
        (Domain, [Contracts]),
        (Persistence, [Contracts, Domain]),
        (Application, [Contracts, Domain, Persistence]),
        (Worker, [Contracts]),

        // Not part of the product: the application the execution slice runs against, so that a
        // browser is never pointed at anybody else's website to build it (ADR-0018).
        (DemoShop, []),
        (Platform, [Contracts]),
        (UiShared, [Contracts, Platform]),
        ("Testo.Website", [Contracts, Platform, UiShared]),
        ("Testo.Modules.*", [Contracts, Platform, UiShared]),
        ("Testo.Capabilities.*", [Contracts, Platform, UiShared]),
        ("Testo.Mocks", [Contracts]),
    ];

    public static TheoryData<string> Projects => [.. SourceProjects.Names];

    public static TheoryData<string> LibraryProjects => [.. SourceProjects.Names.Where(name => name != Host)];

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_References_OnlyWhatItsLayerAllows(string project)
    {
        if (project == Host)
        {
            // The host is the composition root and the only project that may reference everything.
            return;
        }

        var rule = Rules.Where(candidate => Matches(candidate.Project, project)).ToList();
        Assert.True(rule.Count == 1, $"{project} has no dependency rule yet. Add one to {nameof(DependencyRulesTests)} and to docs/architecture.md.");

        var forbidden = SourceProjects.ProjectReferences(project).Except(rule[0].MayReference).ToList();
        Assert.True(forbidden.Count == 0, $"{project} must not reference {string.Join(", ", forbidden)}.");
    }

    [Theory]
    [MemberData(nameof(LibraryProjects))]
    public void Library_HasNoServerFrameworkReference(string project)
    {
        // Only the host depends on the ASP.NET Core server framework. The libraries use the
        // portable component packages, so they could also run in WebAssembly later.
        Assert.Empty(SourceProjects.Load(project).Descendants("FrameworkReference"));
    }

    [Fact]
    public void Contracts_HasNoDependenciesAtAll()
    {
        // The future backend references this project, so it must not pull in anything.
        var project = SourceProjects.Load(Contracts);

        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Theory]
    [InlineData(Contracts)]
    [InlineData("Testo.Mocks")]
    public void NonUiProject_DoesNotUseAnyUiOrWebAssembly(string project)
    {
        var references = Assembly.Load(project).GetReferencedAssemblies().Select(reference => reference.Name ?? "");

        Assert.DoesNotContain(references, name =>
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.FluentUI", StringComparison.Ordinal)
            || name.StartsWith("Testo.UI", StringComparison.Ordinal)
            || name.StartsWith("Testo.Platform", StringComparison.Ordinal));
    }

    [Fact]
    public void Rules_Always_CoverRealProjects()
    {
        // A rule for a project that no longer exists is a sign the rules went stale.
        Assert.All(Rules, rule => Assert.Contains(SourceProjects.Names, project => Matches(rule.Project, project)));
    }

    private static bool Matches(string pattern, string project) =>
        pattern.EndsWith('*')
            ? project.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : pattern == project;
}
