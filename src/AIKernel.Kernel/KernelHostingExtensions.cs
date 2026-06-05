namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Core.Memory;
using AIKernel.Kernel.Memory;
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
        services.TryAddSingleton<IMemoryMapper>(
            _ => OperatingSystem.IsWindows()
                ? new Win32MemoryMapper()
                : new PosixMemoryMapper());
        services.TryAddSingleton<IKernel, Kernel>();
        services.TryAddSingleton<IKernelVersionProvider>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelContextExecutor>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelAttentionAnalyzer>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelMaterialPreprocessor>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelExpressionPreparer>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelProviderRouterAccessor>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelGuardAccessor>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());
        services.TryAddSingleton<IKernelPdpAccessor>(
            serviceProvider => serviceProvider.GetRequiredService<IKernel>());

        return services;
    }
}
