namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class KernelHostingExtensions
{
    public static IServiceCollection AddAIKernelKernel(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IKernelVfsSessionFactory, KernelVfsSessionFactory>();
        services.TryAddSingleton<IKernelModelProviderSelector, StaticKernelModelProviderSelector>();
        services.TryAddSingleton<IKernelRequestHasher, KernelRequestHasher>();
        services.TryAddSingleton<IKernelTransactionIdFactory, KernelTransactionIdFactory>();
        services.TryAddSingleton<IKernel, Kernel>();

        return services;
    }
}
