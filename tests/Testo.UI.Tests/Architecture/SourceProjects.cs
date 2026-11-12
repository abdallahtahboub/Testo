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

    public static string FilePath(string project) => Path.Combine(RepositoryRoot.Source, project, project + ".csproj");

    public static XDocument Load(string project) => XDocument.Load(FilePath(project));

    /// <summary>The projects a project references directly.</summary>
    public static IReadOnlyList<string> ProjectReferences(string project) =>
        [.. Load(project)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? "")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))];
}
