using Microsoft.Extensions.DependencyInjection;
using Testo.Platform.Modules;

namespace Testo.UI.Tests.Platform;

public class ModuleRegistrationTests
{
    [Fact]
    public void AddTestoModule_NewModule_IsAvailableToTheHostAndRegistersItsServices()
    {
        using var provider = new ServiceCollection().AddTestoModule<SampleModule>().BuildServiceProvider();

        var module = Assert.Single(provider.GetServices<IFeatureModule>());
        Assert.IsType<SampleModule>(module);
        Assert.Equal(typeof(SampleModule).Assembly, module.Assembly);
        Assert.NotNull(provider.GetService<SampleService>());
    }

    [Fact]
    public void AddTestoModule_SecondModuleWithTheSameId_Throws()
    {
        var services = new ServiceCollection().AddTestoModule<SampleModule>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddTestoModule<SampleModuleWithSameId>());
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
