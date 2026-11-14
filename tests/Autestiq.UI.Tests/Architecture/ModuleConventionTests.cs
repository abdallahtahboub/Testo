using System.Reflection;
using System.Text.Json;
using Autestiq.Platform.Modules;
using Autestiq.UI.Tests.Support;

namespace Autestiq.UI.Tests.Architecture;

/// <summary>Checks that every feature module follows the conventions of <c>CLAUDE.md</c>.</summary>
public class ModuleConventionTests
{
    public static TheoryData<string> ModuleProjects => [.. SourceProjects.Modules];

    private static IFeatureModule CreateModule(string project)
    {
        var entryPoints = Assembly.Load(project).GetExportedTypes()
            .Where(type => typeof(IFeatureModule).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        Assert.True(entryPoints.Count == 1, $"{project} must expose exactly one public IFeatureModule, but exposes {entryPoints.Count}.");
        return Assert.IsAssignableFrom<IFeatureModule>(Activator.CreateInstance(entryPoints[0]));
    }

    private static string ShortName(string project) => project["Autestiq.Modules.".Length..];

    [Fact]
    public void Modules_Exist()
    {
        Assert.NotEmpty(SourceProjects.Modules);
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_EntryPoint_IsNamedAfterTheProject(string project)
    {
        var module = CreateModule(project);

        Assert.Equal(ShortName(project) + "Module", module.GetType().Name);
        Assert.Equal(project, module.GetType().Namespace);
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_Id_IsTheLowercaseProjectName(string project)
    {
        var id = CreateModule(project).Id;

        Assert.Equal(ShortName(project), id, ignoreCase: true);
        Assert.DoesNotContain(id, char.IsUpper);
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_FeatureFlag_FollowsTheNamingRuleAndIsListedInTheSettings(string project)
    {
        var flag = CreateModule(project).FeatureFlag;

        Assert.Equal("Module." + ShortName(project), flag);
        Assert.Contains(flag, ConfiguredFeatureFlags());
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_IsRegisteredInTheHost(string project)
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot.Source, "Autestiq.Web", "Program.cs"));

        Assert.Contains($"AddAutestiqModule<{ShortName(project)}Module>()", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Modules_Ids_AreUnique()
    {
        var ids = SourceProjects.Modules.Select(project => CreateModule(project).Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static IReadOnlyList<string> ConfiguredFeatureFlags()
    {
        var settings = File.ReadAllText(Path.Combine(RepositoryRoot.Source, "Autestiq.Web", "appsettings.json"));
        using var document = JsonDocument.Parse(settings);
        return [.. document.RootElement.GetProperty("FeatureManagement").EnumerateObject().Select(flag => flag.Name)];
    }
}
