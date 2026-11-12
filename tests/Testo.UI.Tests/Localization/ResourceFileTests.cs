using System.Text.RegularExpressions;
using Testo.UI.Tests.Support;

namespace Testo.UI.Tests.Localization;

/// <summary>
/// Guards the rule "English and German together, never one without the other", and catches
/// keys that are misspelled in code or no longer used.
/// </summary>
public partial class ResourceFileTests
{
    private static readonly string[] SourcePatterns = ["*.razor", "*.cs"];

    public static TheoryData<string> Files => [.. ResourceFile.All.Select(file => file.Name)];

    private static ResourceFile Get(string name) => ResourceFile.All.Single(file => file.Name == name);

    [Fact]
    public void ResourceFiles_Exist()
    {
        Assert.NotEmpty(ResourceFile.All);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ResourceFile_German_HasExactlyTheEnglishKeys(string name)
    {
        var file = Get(name);

        Assert.True(File.Exists(file.GermanPath), $"Missing German file for {name}.");
        Assert.Empty(file.English.Keys.Except(file.German.Keys).Select(key => $"missing in German: {key}"));
        Assert.Empty(file.German.Keys.Except(file.English.Keys).Select(key => $"missing in English: {key}"));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ResourceFile_Texts_AreNeverEmpty(string name)
    {
        var file = Get(name);

        Assert.Empty(file.English.Where(text => string.IsNullOrWhiteSpace(text.Value)).Select(text => text.Key));
        Assert.Empty(file.German.Where(text => string.IsNullOrWhiteSpace(text.Value)).Select(text => text.Key));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ResourceFile_Placeholders_MatchInBothLanguages(string name)
    {
        var file = Get(name);

        var different = file.English
            .Where(text => file.German.TryGetValue(text.Key, out var german)
                && !ResourceFile.Placeholders(text.Value).SequenceEqual(ResourceFile.Placeholders(german)))
            .Select(text => text.Key);

        Assert.Empty(different);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ResourceFile_Keys_FollowTheNamingRule(string name)
    {
        // Area_Element_Purpose, for example DefectList_Filter_Severity.
        Assert.Empty(Get(name).English.Keys.Where(key => !KeyName().IsMatch(key)).Select(key => $"not named Area_Element_Purpose: {key}"));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ResourceFile_KeysUsedInCode_Exist(string name)
    {
        var file = Get(name);

        var unknown = SourceTexts(file)
            .SelectMany(source => LocalizerCall().Matches(source.Text).Select(match => (Key: match.Groups[1].Value, source.File)))
            .Where(use => !file.English.ContainsKey(use.Key))
            .Select(use => $"{use.Key} (used in {RepositoryRoot.Relative(use.File)})")
            .Distinct();

        Assert.Empty(unknown);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ResourceFile_EveryKey_IsUsedInCode(string name)
    {
        var file = Get(name);
        var code = string.Join('\n', SourceTexts(file).Select(source => source.Text));

        Assert.Empty(file.English.Keys.Where(key => !code.Contains($"\"{key}\"", StringComparison.Ordinal)).Select(key => $"never used: {key}"));
    }

    /// <summary>The Razor and C# files of the project a resource file belongs to.</summary>
    private static List<(string File, string Text)> SourceTexts(ResourceFile file) =>
        [.. SourcePatterns
            .SelectMany(pattern => RepositoryRoot.SourceFiles(pattern))
            .Where(source => source.StartsWith(file.ProjectDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(source => (source, File.ReadAllText(source)))];

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*(_[A-Z][A-Za-z0-9]*)+$")]
    private static partial Regex KeyName();

    [GeneratedRegex(@"\bL\[""([^""]+)""")]
    private static partial Regex LocalizerCall();
}
