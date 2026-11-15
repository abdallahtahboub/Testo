using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Autestiq.Mocks.Tests;

public class MockServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAutestiqMocks_Default_ResolvesTheRandomSourceWithTheDefaultSeed()
    {
        using var provider = new ServiceCollection().AddAutestiqMocks().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<MockRandomSource>());
        Assert.Equal(20260101, provider.GetRequiredService<IOptions<MockOptions>>().Value.Seed);
    }

    [Fact]
    public void AddAutestiqMocks_WithConfiguration_AppliesTheOptions()
    {
        using var provider = new ServiceCollection()
            .AddAutestiqMocks(options => options.Seed = 42)
            .BuildServiceProvider();

        Assert.Equal(42, provider.GetRequiredService<IOptions<MockOptions>>().Value.Seed);
    }
}
