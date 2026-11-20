namespace AIKernel.Core.Tests.Vfs;

using AIKernel.Core.Vfs.DependencyInjection;
using AIKernel.Core.Vfs.Local;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Core.Vfs.Web;
using AIKernel.Vfs;
using Microsoft.Extensions.DependencyInjection;

public sealed class VfsDependencyInjectionTests
{
    private sealed class Credentials : IVfsCredentials
    {
        public string? Username => null;
        public string? ApiKey => null;
        public string? Token => null;
        public IReadOnlyDictionary<string, object> Parameters { get; } = new Dictionary<string, object>();
    }

    [Fact]
    public async Task LocalSessionRejectsRootMutationAndListsFileContentsOnDemand()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var options = new LocalFileProviderOptions(root.FullName, allowWrite: true);
            var provider = new LocalFileProvider(options);
            await Assert.ThrowsAsync<AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException>(() => provider.OpenSessionAsync(new Credentials()));
            options.CredentialValidator = _ => true;
            await using var session = await new LocalFileProvider(options).OpenSessionAsync(new Credentials());
            await session.WriteFileAsync("sample.bin", [13, 0, 255]);
            var folder = await session.GetDirectoryAsync("/");
            var file = Assert.Single(await folder.GetFilesAsync());
            Assert.Equal(3, file.Size);
            Assert.Equal(new byte[] { 13, 0, 255 }, await file.ReadAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => session.DeleteAsync("."));
            Assert.True(await session.ExistsAsync("sample.bin"));
        }
        finally { root.Delete(recursive: true); }
    }

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
