using AIKernel.Abstractions.Hosting;
using AIKernel.Abstractions.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AIKernel.Hosting;

public static class DistributedHostingExtensions
{
    /// <summary>Opt in to the registered distributed host and configure application modules before startup.</summary>
    public static AIKernelCoreBuilder WithDistributedApplication(this AIKernelCoreBuilder builder, params IKernelModule[] modules)
    {
        var registrar = new ServiceRegistrar(builder.Services);
        foreach (var module in modules)
        {
            module.ConfigureServices(registrar);
            builder.Services.AddSingleton(module);
        }
        builder.Services.TryAddSingleton<IProviderRegistrar, ModuleProviders>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DistributedLifetime>());
        return builder;
    }

    private sealed class DistributedLifetime(IServiceProvider services, IProviderRegistrar registrar, IEnumerable<IKernelModule> modules) : IHostedService
    {
        private readonly Lazy<IDistributedApplicationHost> host = new(() =>
        {
            foreach (var module in modules) module.ConfigureProviders(registrar);
            return services.GetRequiredService<IDistributedApplicationHost>();
        });
        public Task StartAsync(CancellationToken ct) => host.Value.StartAsync(ct);
        public Task StopAsync(CancellationToken ct) => host.IsValueCreated ? host.Value.StopAsync(ct) : Task.CompletedTask;
    }

    private sealed class ServiceRegistrar(IServiceCollection services) : IServiceRegistrar
    {
        public void AddSingleton<T, TImpl>() where T : class where TImpl : class, T => services.AddSingleton<T,TImpl>();
        public void AddScoped<T, TImpl>() where T : class where TImpl : class, T => services.AddScoped<T,TImpl>();
        public void AddTransient<T, TImpl>() where T : class where TImpl : class, T => services.AddTransient<T,TImpl>();
    }

    private sealed class ModuleProviders : IProviderRegistrar
    {
        private readonly Dictionary<string, Lazy<IProvider>> factories = new(StringComparer.Ordinal);

        public void Register(string providerId, Func<IProvider> factory)
        {
            lock (factories) factories.Add(providerId, new Lazy<IProvider>(factory));
        }

        public bool TryResolve(string providerId, out IProvider? provider)
        {
            Lazy<IProvider>? factory;
            lock (factories) factories.TryGetValue(providerId, out factory);
            provider = factory?.Value;
            return provider != null;
        }

        public IReadOnlyList<string> GetRegisteredIds()
        {
            lock (factories) return factories.Keys.ToArray();
        }
    }
}
