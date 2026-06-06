namespace AIKernel.Core.Tests.Vfs;

using AIKernel.Core.Vfs.DependencyInjection;
using AIKernel.Core.Vfs.Local;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Core.Vfs.Web;
using AIKernel.Vfs;
using Microsoft.Extensions.DependencyInjection;

public sealed class VfsDependencyInjectionTests
{
    [Fact]
    public void AddAIKernelBrowserVfsProviders_RegistersMemoryOnlyByDefault()
    {
        var services = new ServiceCollection();

        services.AddAIKernelBrowserVfsProviders();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalFileProvider));

        using var provider = services.BuildServiceProvider();
        var vfsProviders = provider.GetServices<IVfsProvider>().ToArray();

        var vfsProvider = Assert.Single(vfsProviders);
        Assert.IsType<MemoryFileProvider>(vfsProvider);
    }

    [Fact]
    public void AddAIKernelBrowserVfsProviders_RegistersMemoryAndWebGetWithoutLocalFile()
    {
        var services = new ServiceCollection();

        services.AddAIKernelBrowserVfsProviders(
            webGet: options =>
            {
                options.BaseUri = new Uri("https://example.test/");
            });

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalFileProvider));

        using var provider = services.BuildServiceProvider();
        var vfsProviders = provider.GetServices<IVfsProvider>().ToArray();

        Assert.Contains(vfsProviders, item => item is MemoryFileProvider);
        Assert.Contains(vfsProviders, item => item is WebGetFileProvider);
    }

    [Fact]
    public void AddLocalFileProvider_RegistersConcreteProviderAndContractProvider()
    {
        var services = new ServiceCollection();

        services.AddLocalFileProvider(options =>
        {
            options.RootPath = Path.GetTempPath();
        });

        using var provider = services.BuildServiceProvider();

        var localProvider = provider.GetRequiredService<LocalFileProvider>();
        var vfsProvider = Assert.Single(provider.GetServices<IVfsProvider>());

        Assert.Same(localProvider, vfsProvider);
    }
}
