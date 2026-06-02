namespace AIKernel.Hosting;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Rom;
using AIKernel.Abstractions.Security;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Core.Rom;
using AIKernel.Core.Security;
using AIKernel.Core.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class AIKernelCoreHostingExtensions
{
    public static AIKernelCoreBuilder AddAIKernelCore(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKernelClockDefaults();

        services.AddSecureCredentialProvider(configuration);
        services.AddCoreRuntimeServices();

        return new AIKernelCoreBuilder(services, configuration);
    }

    public static AIKernelCoreBuilder AddAIKernelCore(
        this IServiceCollection services,
        IKernelClock clock,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clock);

        services.AddSingleton(clock);
        services.AddSingleton(clock.Physical);
        services.AddSingleton(clock.Logical);

        services.AddSecureCredentialProvider(configuration);
        services.AddCoreRuntimeServices();

        return new AIKernelCoreBuilder(services, configuration);
    }

    private static IServiceCollection AddKernelClockDefaults(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IKernelClock>(_ => KernelClock.System());
        services.TryAddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IKernelClock>().Physical);
        services.TryAddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IKernelClock>().Logical);

        return services;
    }

    private static IServiceCollection AddSecureCredentialProvider(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        if (configuration is not null)
        {
            services.TryAddSingleton<ISecureCredentialProvider>(
                serviceProvider => new ConfigurationCredentialProvider(
                    configuration,
                    serviceProvider.GetRequiredService<IKernelClock>()));
        }
        else
        {
            services.TryAddSingleton<ISecureCredentialProvider, EnvironmentCredentialProvider>();
        }

        return services;
    }

    private static IServiceCollection AddCoreRuntimeServices(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IMarkdownFrontMatterParser, MarkdownFrontMatterParser>();
        services.TryAddSingleton<IRomCanonicalizer, DefaultRomCanonicalizer>();
        services.TryAddSingleton<ISemanticHasher, Sha256SemanticHasher>();
        services.TryAddSingleton<IRomSignatureVerifier, RomSignatureVerifier>();
        services.TryAddSingleton<IRomLoader, RomLoader>();

        services.TryAddSingleton<IContextCollectionFactory, DefaultContextCollectionFactory>();
        services.TryAddSingleton<IContextHashCalculator, DefaultContextHashCalculator>();
        services.TryAddSingleton<IContextAssemblyGovernancePolicy>(
            _ => new SecurityTagContextAssemblyPolicy(["public", "internal"]));
        services.TryAddSingleton<IContextAssembler, ContextAssembler>();

        services.TryAddSingleton<ITokenizer, SimpleTokenizer>();
        services.TryAddSingleton<IContextPromptProjector, DefaultContextPromptProjector>();
        services.TryAddSingleton<IPromptGenerator, DefaultPromptGenerator>();
        services.TryAddSingleton<IModelPromptCapabilityResolver, StaticModelPromptCapabilityResolver>();
        services.TryAddSingleton<AIKernel.Abstractions.Execution.IKernelExecutor, KernelExecutor>();

        return services;
    }
}
