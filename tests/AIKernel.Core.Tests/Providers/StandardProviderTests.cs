namespace AIKernel.Core.Tests.Providers;

using System.Text;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Capabilities;
using AIKernel.Core.Dsl;
using AIKernel.Core.Providers;
using AIKernel.Core.Providers.LocalExecutionProvider;
using AIKernel.Core.Providers.MinimalRuntimeProvider;
using AIKernel.Core.Providers.SkillProvider;
using AIKernel.Core.Providers.SystemInfoProvider;
using AIKernel.Core.Providers.VfsProvider;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Dtos.Capabilities;
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

public sealed class StandardProviderTests
{
    [Fact]
    public async Task LocalExecutionProviderRegistersInlineExecutionCapability()
    {
        var registry = new InMemoryCapabilityModuleRegistry();
        var provider = new LocalExecutionProvider(registry);

        await provider.InitializeAsync();

        var descriptor = await registry.ResolveAsync(
            "aikernel.local.execute",
            TestContext.Current.CancellationToken);

        Assert.NotNull(descriptor);
        Assert.Equal("Local Execution", descriptor.Name);
        Assert.Equal(["pipeline.execute"], descriptor.ProvidedOperations);
        Assert.Equal("Execution", descriptor.Metadata["kind"]);
        Assert.Equal("Inline", descriptor.Metadata["invocationMode"]);
        Assert.Equal("local,execution,dsl", descriptor.Metadata["tags"]);
    }

    [Fact]
    public async Task LocalExecutionInvokerExecutesDslPipelineDeterministically()
    {
        var capabilityRegistry = new DslRomCapabilityRegistry(
            new FailClosedDslCapabilityRegistry(),
            new DslRomRegistry());
        var compiler = new DslPipelineCompiler(capabilityRegistry);
        var invoker = new LocalExecutionInvoker(compiler);

        var result = await invoker.InvokeAsync(
            new CapabilityInvocationRequest(
                "invoke-local",
                "aikernel.local.execute",
                "pipeline.execute",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["pipeline.json"] =
                        """
                        {
                          "type": "Pipeline",
                          "steps": [
                            { "type": "Step", "name": "read input" },
                            { "type": "Step", "name": "return summary" }
                          ]
                        }
                        """,
                    ["input.text"] = "hello"
                },
                InputHash: null,
                ReplayLogHash: null,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.OutputHash);
        Assert.Equal("return summary", result.Metadata["dsl.current_node"]);
        Assert.Equal("2", result.Metadata["dsl.executed_node_count"]);
        Assert.Equal("hello", result.Metadata["output.text"]);
    }

    [Fact]
    public async Task LocalExecutionInvokerFailsClosedForInvalidPipelineJson()
    {
        var capabilityRegistry = new DslRomCapabilityRegistry(
            new FailClosedDslCapabilityRegistry(),
            new DslRomRegistry());
        var compiler = new DslPipelineCompiler(capabilityRegistry);
        var invoker = new LocalExecutionInvoker(compiler);

        var result = await invoker.InvokeAsync(
            new CapabilityInvocationRequest(
                "invoke-invalid-local",
                "aikernel.local.execute",
                "pipeline.execute",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["pipeline.json"] =
                        """
                        {
                          "type": "Pipeline",
                          "steps": [
                            { "type": "Loop", "body": [] }
                          ]
                        }
                        """
                },
                InputHash: null,
                ReplayLogHash: null,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("LOCAL_EXECUTION_INVALID_PIPELINE", result.ErrorCode);
        Assert.Contains("maxIterations", result.ErrorMessage);
    }

    [Fact]
    public async Task MinimalRuntimeProviderRegistersPingCapability()
    {
        var registry = new InMemoryCapabilityModuleRegistry();
        var provider = new MinimalRuntimeProvider(registry);

        await provider.InitializeAsync();

        var descriptor = await registry.ResolveAsync(
            "aikernel.runtime.ping",
            TestContext.Current.CancellationToken);

        Assert.NotNull(descriptor);
        Assert.Equal("Minimal Runtime Ping", descriptor.Name);
        Assert.Equal(["runtime.ping"], descriptor.ProvidedOperations);
        Assert.Equal("Utility", descriptor.Metadata["kind"]);
        Assert.Equal("Inline", descriptor.Metadata["invocationMode"]);
        Assert.Equal("runtime,minimal,ping", descriptor.Metadata["tags"]);
    }

    [Fact]
    public async Task MinimalRuntimeInvokerReturnsDeterministicNoOpSuccess()
    {
        var invoker = new MinimalRuntimeInvoker();

        var result = await invoker.InvokeAsync(
            new CapabilityInvocationRequest(
                "invoke-runtime",
                "aikernel.runtime.ping",
                "runtime.ping",
                new Dictionary<string, string>(StringComparer.Ordinal),
                InputHash: null,
                ReplayLogHash: "replay",
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.OutputHash);
        Assert.Null(result.ErrorCode);
        Assert.Equal("ok", result.Metadata["status"]);
        Assert.Equal("replay", result.ReplayLogHash);
    }

    [Fact]
    public async Task VfsProviderRegistersReadOnlyCapability()
    {
        var registry = new InMemoryCapabilityModuleRegistry();
        var provider = new VfsProvider(registry);

        await provider.InitializeAsync();

        var descriptor = await registry.ResolveAsync(
            "aikernel.vfs",
            TestContext.Current.CancellationToken);

        Assert.NotNull(descriptor);
        Assert.Equal("Virtual File System Read", descriptor.Name);
        Assert.Equal(["vfs.exists", "vfs.list", "vfs.metadata", "vfs.read_file"], descriptor.ProvidedOperations);
        Assert.Equal("Storage", descriptor.Metadata["kind"]);
        Assert.Equal("Inline", descriptor.Metadata["invocationMode"]);
        Assert.Equal("vfs,filesystem,storage,core", descriptor.Metadata["tags"]);
    }

    [Fact]
    public async Task VfsInvokerReadsListsExistsAndReturnsMetadata()
    {
        var memory = new MemoryFileProvider();
        memory.Seed("/skills/demo/SKILL.md", Encoding.UTF8.GetBytes("hello skill"));
        var invoker = new VfsInvoker([memory]);

        var read = await InvokeVfsAsync(
            invoker,
            "vfs.read_file",
            "/skills/demo/SKILL.md");
        Assert.True(read.Succeeded, read.ErrorMessage);
        Assert.Equal("true", read.Metadata["exists"]);
        Assert.Equal("hello skill", read.Metadata["content"]);
        Assert.Equal("11", read.Metadata["size"]);

        var exists = await InvokeVfsAsync(
            invoker,
            "vfs.exists",
            "/skills/demo/SKILL.md");
        Assert.Equal("true", exists.Metadata["exists"]);

        var list = await InvokeVfsAsync(
            invoker,
            "vfs.list",
            "/skills");
        Assert.True(list.Succeeded, list.ErrorMessage);
        Assert.Contains("SKILL.md", list.Metadata["entries.json"]);

        var metadata = await InvokeVfsAsync(
            invoker,
            "vfs.metadata",
            "/skills/demo/SKILL.md");
        Assert.Equal("file", metadata.Metadata["type"]);
        Assert.Equal("11", metadata.Metadata["size"]);
    }

    [Fact]
    public async Task SystemInfoProviderRegistersIntrospectionCapability()
    {
        var registry = new InMemoryCapabilityModuleRegistry();
        var provider = new SystemInfoProvider(registry);

        await provider.InitializeAsync();

        var descriptor = await registry.ResolveAsync(
            "aikernel.system.info",
            TestContext.Current.CancellationToken);

        Assert.NotNull(descriptor);
        Assert.Equal("System Information", descriptor.Name);
        Assert.Equal(
            ["system.capabilities", "system.info", "system.providers", "system.runtime", "system.vfs"],
            descriptor.ProvidedOperations);
        Assert.Equal("Utility", descriptor.Metadata["kind"]);
        Assert.Equal("Inline", descriptor.Metadata["invocationMode"]);
        Assert.Equal("system,info,introspection,core", descriptor.Metadata["tags"]);
    }

    [Fact]
    public async Task SystemInfoInvokerReturnsSafeStructuredSummaries()
    {
        var capabilityRegistry = new InMemoryCapabilityModuleRegistry();
        await capabilityRegistry.RegisterAsync(
            SystemInfoCapabilityContracts.ToContract(SystemInfoCapabilityDescriptor.Standard()),
            TestContext.Current.CancellationToken);
        var systemProvider = new SystemInfoProvider(capabilityRegistry);
        var runtimeProvider = new MinimalRuntimeProvider(capabilityRegistry);
        var invoker = new SystemInfoInvoker(
            [systemProvider, runtimeProvider],
            capabilityRegistry,
            [new MemoryFileProvider()]);

        foreach (var operation in new[]
                 {
                     "system.info",
                     "system.providers",
                     "system.capabilities",
                     "system.vfs",
                     "system.runtime"
                 })
        {
            var result = await InvokeSystemAsync(invoker, operation);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Contains("snapshot.json", result.Metadata.Keys);
            Assert.NotNull(result.OutputHash);
        }
    }

    [Fact]
    public void CoreRegistersOsLevelStandardProviders()
    {
        var services = new ServiceCollection();
        services.AddAIKernelCore();
        using var provider = services.BuildServiceProvider();

        var providerIds = provider
            .GetServices<IProvider>()
            .Select(x => x.ProviderId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("aikernel.local", providerIds);
        Assert.Contains("aikernel.runtime.minimal", providerIds);
        Assert.Contains("aikernel.skill", providerIds);
        Assert.Contains("aikernel.system", providerIds);
        Assert.Contains("aikernel.vfs", providerIds);
        Assert.NotNull(provider.GetRequiredService<LocalExecutionInvoker>());
        Assert.NotNull(provider.GetRequiredService<MinimalRuntimeInvoker>());
        Assert.NotNull(provider.GetRequiredService<VfsInvoker>());
        Assert.NotNull(provider.GetRequiredService<SystemInfoInvoker>());
        Assert.NotNull(provider.GetRequiredService<IDynamicProviderRegistry>());
        var invokerTypes = provider
            .GetServices<ICapabilityModuleInvoker>()
            .Select(x => x.GetType())
            .ToArray();
        Assert.Contains(typeof(LocalExecutionInvoker), invokerTypes);
        Assert.Contains(typeof(MinimalRuntimeInvoker), invokerTypes);
        Assert.Contains(typeof(VfsInvoker), invokerTypes);
        Assert.Contains(typeof(SystemInfoInvoker), invokerTypes);
        Assert.Contains(typeof(SkillProvider), invokerTypes);
        Assert.Contains(typeof(FailClosedCapabilityModuleInvoker), invokerTypes);
        var dynamicInvokerTypes = provider
            .GetRequiredService<IDynamicProviderRegistry>()
            .GetRegisteredInvokers()
            .Select(x => x.GetType())
            .ToArray();
        Assert.Contains(typeof(LocalExecutionInvoker), dynamicInvokerTypes);
        Assert.Contains(typeof(MinimalRuntimeInvoker), dynamicInvokerTypes);
        Assert.Contains(typeof(VfsInvoker), dynamicInvokerTypes);
        Assert.Contains(typeof(SystemInfoInvoker), dynamicInvokerTypes);
        Assert.Contains(typeof(SkillProvider), dynamicInvokerTypes);
        Assert.Contains(typeof(FailClosedCapabilityModuleInvoker), dynamicInvokerTypes);
        Assert.Equal(
            MinimalRuntimeProvider.ProviderIdValue,
            provider.GetRequiredService<MinimalRuntimeProvider>().ProviderId);
        Assert.Equal(
            VfsProvider.ProviderIdValue,
            provider.GetRequiredService<VfsProvider>().ProviderId);
    }

    [Fact]
    public void CoreProvidesDynamicRegistryWhenHostOverridesStableProviderRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProviderRegistry, StableOnlyProviderRegistry>();
        services.AddAIKernelCore();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<StableOnlyProviderRegistry>(provider.GetRequiredService<IProviderRegistry>());
        Assert.IsType<InMemoryProviderRegistry>(provider.GetRequiredService<IDynamicProviderRegistry>());
    }

    private static async Task<CapabilityInvocationResult> InvokeVfsAsync(
        VfsInvoker invoker,
        string operation,
        string path)
    {
        return await invoker.InvokeAsync(
            new CapabilityInvocationRequest(
                $"invoke-{operation}",
                "aikernel.vfs",
                operation,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = path
                },
                InputHash: null,
                ReplayLogHash: null,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);
    }

    private static async Task<CapabilityInvocationResult> InvokeSystemAsync(
        SystemInfoInvoker invoker,
        string operation)
    {
        return await invoker.InvokeAsync(
            new CapabilityInvocationRequest(
                $"invoke-{operation}",
                "aikernel.system.info",
                operation,
                new Dictionary<string, string>(StringComparer.Ordinal),
                InputHash: null,
                ReplayLogHash: null,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);
    }

    private sealed class StableOnlyProviderRegistry : IProviderRegistry
    {
        public void RegisterProvider(
            string name,
            IProvider provider)
        {
        }

        public bool UnregisterProvider(
            string name)
        {
            return false;
        }

        public IReadOnlyList<string> GetRegisteredProviders()
        {
            return [];
        }
    }
}
