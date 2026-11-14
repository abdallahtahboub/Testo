using System.Xml.Linq;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Architecture;

/// <summary>Reads the project files below <c>src</c>.</summary>
internal static class SourceProjects
{
    /// <summary>The names of all projects below <c>src</c>, for example <c>Testo.Platform</c>.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [.. RepositoryRoot.SourceFiles("*.csproj").Select(file => Path.GetFileNameWithoutExtension(file)).Order(StringComparer.Ordinal)];

    /// <summary>The names of the feature module projects.</summary>
    public static IReadOnlyList<string> Modules { get; } =
        [.. Names.Where(name => name.StartsWith("Testo.Modules.", StringComparison.Ordinal))];

    /// <summary>
    /// The projects whose assemblies these tests can actually load: the ones this test project
    /// references, directly or not.
    /// </summary>
    /// <remarks>
    /// Not every project is one of them. The worker and the demo shop are deliberately not
    /// referenced by the user interface tests — the worker is treated as untrusted and the demo
    /// shop is a target, not part of the product (ADR-0018) — so a test that reflects over types
    /// has to skip them rather than fail on them. The rule tests read the project files instead,
    /// so they still cover every project.
    /// </remarks>
    public static IReadOnlyList<System.Reflection.Assembly> LoadableAssemblies { get; } =
        [.. Names.Select(TryLoad).OfType<System.Reflection.Assembly>()];

    private static System.Reflection.Assembly? TryLoad(string project)
    {
        try
        {
            return System.Reflection.Assembly.Load(project);
        }
        catch (Exception exception) when (exception is FileNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    public static string FilePath(string project) => Path.Combine(RepositoryRoot.Source, project, project + ".csproj");

    public static XDocument Load(string project) => XDocument.Load(FilePath(project));

    /// <summary>The projects a project references directly.</summary>
    public static IReadOnlyList<string> ProjectReferences(string project) =>
        [.. Load(project)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? "")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))];
}
