using Microsoft.Extensions.DependencyInjection;
using Autestiq.Platform.Modules;

namespace Autestiq.UI.Tests.Platform;

public class ModuleRegistrationTests
{
    [Fact]
    public void AddAutestiqModule_NewModule_IsAvailableToTheHostAndRegistersItsServices()
    {
        using var provider = new ServiceCollection().AddAutestiqModule<SampleModule>().BuildServiceProvider();

        var module = Assert.Single(provider.GetServices<IFeatureModule>());
        Assert.IsType<SampleModule>(module);
        Assert.Equal(typeof(SampleModule).Assembly, module.Assembly);
        Assert.NotNull(provider.GetService<SampleService>());
    }

    [Fact]
    public void AddAutestiqModule_SecondModuleWithTheSameId_Throws()
    {
        var services = new ServiceCollection().AddAutestiqModule<SampleModule>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddAutestiqModule<SampleModuleWithSameId>());
        Assert.Contains("'sample'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SampleService;

    private sealed class SampleModule : IFeatureModule
    {
        public string Id => "sample";

        public string? FeatureFlag => "Module.Sample";

        public void RegisterServices(IServiceCollection services) => services.AddSingleton<SampleService>();
    }

    private sealed class SampleModuleWithSameId : IFeatureModule
    {
        public string Id => "Sample";

        public string? FeatureFlag => null;
    }
}
