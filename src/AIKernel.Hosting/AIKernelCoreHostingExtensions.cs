namespace AIKernel.Hosting;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Rom;
using AIKernel.Abstractions.Security;
using AIKernel.Core.Capabilities;
using AIKernel.Core.ChatHistory;
using AIKernel.Core.Control;
using AIKernel.Core.Context;
using AIKernel.Core.Dsl;
using AIKernel.Core.Execution;
using AIKernel.Core.Governance.ChatChain;
using AIKernel.Core.Providers;
using AIKernel.Core.Providers.LocalExecutionProvider;
using AIKernel.Core.Providers.MinimalRuntimeProvider;
using AIKernel.Core.Providers.SkillProvider;
using AIKernel.Core.Providers.SystemInfoProvider;
using AIKernel.Core.Providers.VfsProvider;
using AIKernel.Core.Rom;
using AIKernel.Core.Routing;
using AIKernel.Core.Security;
using AIKernel.Core.Time;
using AIKernel.Dtos.Rom;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreHostingExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreHostingExtensions']/summary" />
public static class AIKernelCoreHostingExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreHostingExtensions.AddAIKernelCore']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreHostingExtensions.AddAIKernelCore']/summary" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreHostingExtensions.AddAIKernelCore']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreHostingExtensions.AddAIKernelCore']/summary" />
    public static AIKernelCoreBuilder AddAIKernelCore(
        this IServiceCollection services,
        IKernelClock clock,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clock);

        services.AddSingleton(clock);
        services.AddSingleton<AIKernel.Abstractions.Time.IKernelClock>(
            _ => (AIKernel.Abstractions.Time.IKernelClock)clock);
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
        services.TryAddSingleton<AIKernel.Abstractions.Time.IKernelClock>(
            serviceProvider => (AIKernel.Abstractions.Time.IKernelClock)
                serviceProvider.GetRequiredService<IKernelClock>());
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
        services.TryAddSingleton<IHistoryRomRegistry, HistoryRomRegistry>();
        services.TryAddSingleton<AIKernel.Abstractions.History.IHistoryRomRegistry>(
            serviceProvider => (AIKernel.Abstractions.History.IHistoryRomRegistry)
                serviceProvider.GetRequiredService<IHistoryRomRegistry>());
        services.TryAddSingleton<HistoryRomProvider>();
        services.TryAddSingleton<AIKernel.Abstractions.History.IChatHistoryRomExporter, ChatHistoryRomExporter>();
        services.TryAddSingleton<HistoryRomStore>();
        services.TryAddSingleton<AIKernel.Abstractions.History.IHistoryRomStore>(
            serviceProvider => serviceProvider.GetRequiredService<HistoryRomStore>());

        services.TryAddSingleton<IDslRomRegistry, DslRomRegistry>();
        services.TryAddSingleton<FailClosedDslCapabilityRegistry>();
        services.TryAddSingleton<IDslCapabilityRegistry>(
            serviceProvider => new DslRomCapabilityRegistry(
                serviceProvider.GetRequiredService<FailClosedDslCapabilityRegistry>(),
                serviceProvider.GetRequiredService<IDslRomRegistry>()));
        services.TryAddSingleton<IDslPipelineCompiler, DslPipelineCompiler>();
        services.TryAddSingleton<DslRomProvider>();
        services.TryAddSingleton<DslRomStore>();
        services.TryAddSingleton<AIKernel.Abstractions.Dsl.IDslRomRegistry>(
            serviceProvider => (AIKernel.Abstractions.Dsl.IDslRomRegistry)
                serviceProvider.GetRequiredService<IDslRomRegistry>());
        services.TryAddSingleton<AIKernel.Abstractions.Dsl.IDslCapabilityRegistry>(
            serviceProvider => (AIKernel.Abstractions.Dsl.IDslCapabilityRegistry)
                serviceProvider.GetRequiredService<IDslCapabilityRegistry>());
        services.TryAddSingleton<AIKernel.Abstractions.Dsl.IDslPipelineCompiler>(
            serviceProvider => (AIKernel.Abstractions.Dsl.IDslPipelineCompiler)
                serviceProvider.GetRequiredService<IDslPipelineCompiler>());
        services.TryAddSingleton<AIKernel.Abstractions.Dsl.IDslRomStore>(
            serviceProvider => serviceProvider.GetRequiredService<DslRomStore>());

        services.TryAddSingleton<IContextCollectionFactory, DefaultContextCollectionFactory>();
        services.TryAddSingleton<IContextHashCalculator, DefaultContextHashCalculator>();
        services.TryAddSingleton<IRomPathResolver>(
            _ => new DictionaryRomPathResolver(new Dictionary<RomId, string>()));
        services.TryAddSingleton<IContextAssemblyGovernancePolicy>(
            _ => new SecurityTagContextAssemblyPolicy(["public", "internal"]));
        services.TryAddSingleton<IContextAssembler, ContextAssembler>();

        services.TryAddSingleton<ITokenizer, SimpleTokenizer>();
        services.TryAddSingleton<IContextPromptProjector, DefaultContextPromptProjector>();
        services.TryAddSingleton<IPromptGenerator, DefaultPromptGenerator>();
        services.TryAddSingleton<IModelPromptCapabilityResolver, StaticModelPromptCapabilityResolver>();
        services.TryAddSingleton<AIKernel.Abstractions.Execution.IKernelExecutor, KernelExecutor>();
        services.TryAddSingleton<IKernelReplayer, KernelReplayer>();
        services.TryAddSingleton<IPipelineOrchestrator, PipelineOrchestrator>();
        services.TryAddSingleton<IOutputPolisher, PassThroughOutputPolisher>();
        services.TryAddSingleton<IPolisherValidator, DefaultPolisherValidator>();

        services.TryAddSingleton<AIKernel.Abstractions.Governance.ChatChain.IChatTurnCanonicalizer,
            DefaultChatTurnCanonicalizer>();
        services.TryAddSingleton<AIKernel.Abstractions.Governance.ChatChain.IChatTurnSemanticHasher,
            Sha256ChatTurnSemanticHasher>();
        services.TryAddSingleton<AIKernel.Abstractions.Governance.ChatChain.IChatTurnSignatureProvider,
            AlgorithmTaggedChatTurnSignatureProvider>();
        services.TryAddSingleton<AIKernel.Abstractions.Governance.ChatChain.IChatTurnChainVerifier,
            ChatTurnChainVerifier>();
        services.TryAddSingleton<AIKernel.Abstractions.Routing.ICapabilityRegistry,
            InMemoryCapabilityRegistry>();
        services.TryAddSingleton<AIKernel.Abstractions.Routing.ISemanticRouter,
            PassThroughSemanticRouter>();
        services.TryAddSingleton<RuleEvaluator>();
        services.TryAddSingleton(
            serviceProvider => new BonsaiEngine(
                serviceProvider.GetRequiredService<RuleEvaluator>(),
                serviceProvider.GetService<AIKernel.Abstractions.Events.IEventBus>()));
        services.TryAddSingleton<IBonsaiEngine>(
            serviceProvider => serviceProvider.GetRequiredService<BonsaiEngine>());
        services.TryAddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry,
            InMemoryCapabilityModuleRegistry>();
        services.TryAddSingleton<SkillManifestParser>();
        services.TryAddSingleton<SkillLoader>();
        services.TryAddSingleton<LocalExecutionInvoker>();
        services.TryAddSingleton<MinimalRuntimeInvoker>();
        services.TryAddSingleton<VfsInvoker>();
        services.TryAddSingleton<SystemInfoInvoker>();
        services.TryAddSingleton<LocalExecutionProvider>(
            serviceProvider => new LocalExecutionProvider(
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>()));
        services.TryAddSingleton<MinimalRuntimeProvider>(
            serviceProvider => new MinimalRuntimeProvider(
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>()));
        services.TryAddSingleton<VfsProvider>(
            serviceProvider => new VfsProvider(
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>()));
        services.TryAddSingleton<SystemInfoProvider>(
            serviceProvider => new SystemInfoProvider(
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>()));
        services.TryAddSingleton<SkillProvider>(
            serviceProvider => new SkillProvider(
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>(),
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Dsl.IDslPipelineCompiler>(),
                serviceProvider.GetRequiredService<SkillLoader>()));
        services.AddSingleton<AIKernel.Abstractions.Providers.IProvider>(
            serviceProvider => serviceProvider.GetRequiredService<LocalExecutionProvider>());
        services.AddSingleton<AIKernel.Abstractions.Providers.IProvider>(
            serviceProvider => serviceProvider.GetRequiredService<MinimalRuntimeProvider>());
        services.AddSingleton<AIKernel.Abstractions.Providers.IProvider>(
            serviceProvider => serviceProvider.GetRequiredService<VfsProvider>());
        services.AddSingleton<AIKernel.Abstractions.Providers.IProvider>(
            serviceProvider => serviceProvider.GetRequiredService<SystemInfoProvider>());
        services.AddSingleton<AIKernel.Abstractions.Providers.IProvider>(
            serviceProvider => serviceProvider.GetRequiredService<SkillProvider>());
        services.AddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>(
            serviceProvider => serviceProvider.GetRequiredService<LocalExecutionInvoker>());
        services.AddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>(
            serviceProvider => serviceProvider.GetRequiredService<MinimalRuntimeInvoker>());
        services.AddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>(
            serviceProvider => serviceProvider.GetRequiredService<VfsInvoker>());
        services.AddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>(
            serviceProvider => serviceProvider.GetRequiredService<SystemInfoInvoker>());
        services.AddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>(
            serviceProvider => serviceProvider.GetRequiredService<SkillProvider>());
        services.TryAddSingleton<AIKernel.Abstractions.Providers.IProviderRegistry>(
            serviceProvider => new InMemoryProviderRegistry(
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>(),
                serviceProvider.GetServices<AIKernel.Abstractions.Providers.IProvider>(),
                serviceProvider.GetServices<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>()));
        services.TryAddSingleton<IDynamicProviderRegistry>(
            serviceProvider =>
                serviceProvider.GetRequiredService<AIKernel.Abstractions.Providers.IProviderRegistry>()
                    is IDynamicProviderRegistry dynamicProviderRegistry
                    ? dynamicProviderRegistry
                    : new InMemoryProviderRegistry(
                        serviceProvider.GetRequiredService<AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>(),
                        serviceProvider.GetServices<AIKernel.Abstractions.Providers.IProvider>(),
                        serviceProvider.GetServices<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>()));
        services.AddSingleton<AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker,
            FailClosedCapabilityModuleInvoker>();

        return services;
    }
}
