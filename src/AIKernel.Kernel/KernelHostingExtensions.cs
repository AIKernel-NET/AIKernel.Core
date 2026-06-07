namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Memory;
using AIKernel.Core.Memory;
using AIKernel.Kernel.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelHostingExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelHostingExtensions']" />
public static class KernelHostingExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelHostingExtensions.AddAIKernelKernel']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelHostingExtensions.AddAIKernelKernel']" />
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
