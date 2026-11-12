using System.Text.RegularExpressions;
using System.Xml.Linq;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Localization;

/// <summary>One English resource file together with its German translation.</summary>
internal sealed partial class ResourceFile
{
    private ResourceFile(string path)
    {
        EnglishPath = path;
        GermanPath = Path.ChangeExtension(path, ".de.resx");
        English = Read(EnglishPath);
        German = File.Exists(GermanPath) ? Read(GermanPath) : new Dictionary<string, string>();
    }

    public string EnglishPath { get; }

    public string GermanPath { get; }

    public IReadOnlyDictionary<string, string> English { get; }

    public IReadOnlyDictionary<string, string> German { get; }

    /// <summary>The folder of the project the file belongs to (the parent of its <c>Resources</c> folder).</summary>
    public string ProjectDirectory => Path.GetDirectoryName(Path.GetDirectoryName(EnglishPath)!)!;

    public string Name => RepositoryRoot.Relative(EnglishPath);

    /// <summary>All English resource files below <c>src</c>.</summary>
    public static IReadOnlyList<ResourceFile> All { get; } =
        [.. RepositoryRoot.SourceFiles("*.resx")
            .Where(file => !CultureSuffix().IsMatch(file))
            .Order(StringComparer.Ordinal)
            .Select(file => new ResourceFile(file))];

    /// <summary>The <c>{0}</c>-style placeholders of a text.</summary>
    public static IReadOnlyList<string> Placeholders(string text) =>
        [.. Placeholder().Matches(text).Select(match => match.Value).Distinct().Order(StringComparer.Ordinal)];

    private static Dictionary<string, string> Read(string path) =>
        XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "");

    [GeneratedRegex(@"\.[a-z]{2}(-[A-Z]{2})?\.resx$")]
    private static partial Regex CultureSuffix();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();
}
