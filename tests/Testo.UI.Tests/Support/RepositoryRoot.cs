namespace Testo.UI.Tests.Support;

/// <summary>Finds the repository on disk, for tests that check source files.</summary>
internal static class RepositoryRoot
{
    private static readonly Lazy<string> Root = new(Find);

    /// <summary>The folder that contains <c>Testo.slnx</c>.</summary>
    public static string Path => Root.Value;

    /// <summary>The <c>src</c> folder.</summary>
    public static string Source => System.IO.Path.Combine(Path, "src");

    /// <summary>All files below <c>src</c> that match the pattern, without build output.</summary>
    public static IEnumerable<string> SourceFiles(string searchPattern) =>
        Directory.EnumerateFiles(Source, searchPattern, SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file));

    /// <summary>A path relative to the repository root, with forward slashes, for readable messages.</summary>
    public static string Relative(string file) =>
        System.IO.Path.GetRelativePath(Path, file).Replace('\\', '/');

    private static bool IsBuildOutput(string file)
    {
        var relative = Relative(file);
        return relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Testo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find Testo.slnx above " + AppContext.BaseDirectory);
    }
}
