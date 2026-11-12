using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Testo.Mocks.Tests;

public class MockServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTestoMocks_Default_ResolvesTheRandomSourceWithTheDefaultSeed()
    {
        using var provider = new ServiceCollection().AddTestoMocks().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<MockRandomSource>());
        Assert.Equal(20260101, provider.GetRequiredService<IOptions<MockOptions>>().Value.Seed);
    }

    [Fact]
    public void AddTestoMocks_WithConfiguration_AppliesTheOptions()
    {
        using var provider = new ServiceCollection()
            .AddTestoMocks(options => options.Seed = 42)
            .BuildServiceProvider();

        Assert.Equal(42, provider.GetRequiredService<IOptions<MockOptions>>().Value.Seed);
    }
}
