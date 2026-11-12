using Microsoft.Extensions.Options;

namespace Testo.Mocks.Tests;

public class MockRandomSourceTests
{
    private static MockRandomSource CreateSource(int seed = 20260101) =>
        new(Options.Create(new MockOptions { Seed = seed }));

    private static int[] Take(Random random, int count) =>
        [.. Enumerable.Range(0, count).Select(_ => random.Next())];

    [Fact]
    public void Create_SameSeedAndStream_ProducesTheSameSequence()
    {
        var first = Take(CreateSource().Create("projects"), 20);
        var second = Take(CreateSource().Create("projects"), 20);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_DifferentStreams_ProduceDifferentSequences()
    {
        var source = CreateSource();

        Assert.NotEqual(Take(source.Create("projects"), 20), Take(source.Create("defects"), 20));
    }

    [Fact]
    public void Create_DifferentSeeds_ProduceDifferentSequences()
    {
        Assert.NotEqual(Take(CreateSource(1).Create("projects"), 20), Take(CreateSource(2).Create("projects"), 20));
    }

    [Fact]
    public void StableHash_KnownInput_NeverChanges()
    {
        // Pinned value: if this fails, the hash changed and all mock data would silently shift.
        Assert.Equal(-2041290654, MockRandomSource.StableHash(20260101, "projects"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_BlankStreamName_Throws(string stream)
    {
        Assert.Throws<ArgumentException>(() => CreateSource().Create(stream));
    }
}
