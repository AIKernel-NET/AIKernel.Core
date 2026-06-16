namespace AIKernel.IntegrationTests;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Rom;
using AIKernel.Core.ChatHistory;
using AIKernel.Core.Context;
using AIKernel.Core.Dsl;
using AIKernel.Dtos.Context;
using AIKernel.Hosting;
using AIKernel.Kernel;
using Microsoft.Extensions.DependencyInjection;

public sealed class CoreHostingExtensionsTests
{
    [Fact]
    public void AddAIKernelKernel_RegistersKernelFacadeAndAccessors()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();
        services.AddAIKernelKernel();

        using var provider = services.BuildServiceProvider();

        var kernel = provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernel>();

        Assert.NotNull(kernel);
        Assert.NotNull(kernel.GetProviderRouter());
        Assert.NotNull(kernel.GetGuard());
        Assert.NotNull(kernel.GetPdp());
    }

    [Fact]
    public void AddAIKernelKernel_RegistersKernelCapabilityInterfaces()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();
        services.AddAIKernelKernel();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var kernel = provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernel>();

        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelVersionProvider>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelContextExecutor>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelAttentionAnalyzer>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelMaterialPreprocessor>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelExpressionPreparer>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelProviderRouterAccessor>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelGuardAccessor>());
        Assert.Same(
            kernel,
            provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernelPdpAccessor>());
    }

    [Fact]
    public void AddAIKernelCore_RegistersHistoryRomServices()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHistoryRomRegistry>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.History.IHistoryRomRegistry>());
        Assert.NotNull(provider.GetRequiredService<HistoryRomProvider>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.History.IChatHistoryRomExporter>());
        Assert.NotNull(provider.GetRequiredService<HistoryRomStore>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.History.IHistoryRomStore>());
    }

    [Fact]
    public void AddAIKernelCore_RegistersDslRomServices()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IDslRomRegistry>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Dsl.IDslRomRegistry>());
        Assert.NotNull(provider.GetRequiredService<IDslCapabilityRegistry>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Dsl.IDslCapabilityRegistry>());
        Assert.NotNull(provider.GetRequiredService<IDslPipelineCompiler>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Dsl.IDslPipelineCompiler>());
        Assert.NotNull(provider.GetRequiredService<DslRomProvider>());
        Assert.NotNull(provider.GetRequiredService<DslRomStore>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Dsl.IDslRomStore>());
    }

    [Fact]
    public void AddAIKernelCore_RegistersCoreRuntimeContracts()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IMarkdownFrontMatterParser>());
        Assert.NotNull(provider.GetRequiredService<IRomCanonicalizer>());
        Assert.NotNull(provider.GetRequiredService<ISemanticHasher>());
        Assert.NotNull(provider.GetRequiredService<IRomSignatureVerifier>());
        Assert.NotNull(provider.GetRequiredService<IRomLoader>());
        Assert.NotNull(provider.GetRequiredService<IContextCollectionFactory>());
        Assert.NotNull(provider.GetRequiredService<IContextHashCalculator>());
        Assert.NotNull(provider.GetRequiredService<IRomPathResolver>());
        Assert.NotNull(provider.GetRequiredService<IContextAssemblyGovernancePolicy>());
        Assert.NotNull(provider.GetRequiredService<IContextAssembler>());
        Assert.NotNull(provider.GetRequiredService<ITokenizer>());
        Assert.NotNull(provider.GetRequiredService<IContextPromptProjector>());
        Assert.NotNull(provider.GetRequiredService<IPromptGenerator>());
        Assert.NotNull(provider.GetRequiredService<IModelPromptCapabilityResolver>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Execution.IKernelExecutor>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Execution.IKernelReplayer>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Execution.IPipelineOrchestrator>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Execution.IOutputPolisher>());
        Assert.NotNull(provider.GetRequiredService<AIKernel.Abstractions.Execution.IPolisherValidator>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Governance.ChatChain.IChatTurnCanonicalizer>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Governance.ChatChain.IChatTurnSemanticHasher>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Governance.ChatChain.IChatTurnSignatureProvider>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Governance.ChatChain.IChatTurnChainVerifier>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Providers.IProviderRegistry>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Routing.ICapabilityRegistry>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Capabilities.ICapabilityModuleRegistry>());
        Assert.NotNull(provider.GetRequiredService<
            AIKernel.Abstractions.Capabilities.ICapabilityModuleInvoker>());
    }

    [Fact]
    public async Task AddAIKernelKernel_FailsClosed_ForLegacyContextExecution()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();
        services.AddAIKernelKernel();

        using var provider = services.BuildServiceProvider();

        var kernel = (AIKernel.Kernel.Kernel)provider
            .GetRequiredService<AIKernel.Abstractions.Kernel.IKernel>();

        var result = await kernel.ExecuteAsync(new UnifiedContextDto
        {
            Id = "legacy-context",
            Orchestration = new OrchestrationContextDto
            {
                Purpose = "legacy",
                Structure = "legacy"
            }
        });

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(
            "KernelRequest execution is required for the AIKernel.Core pipeline.",
            result.Error);
    }
}
