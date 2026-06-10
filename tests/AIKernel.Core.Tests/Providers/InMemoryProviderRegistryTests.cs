namespace AIKernel.Core.Tests.Providers;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Capabilities;
using AIKernel.Core.Providers;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;
using Xunit;

public sealed class InMemoryProviderRegistryTests
{
    [Fact]
    public void GetRegisteredProviders_ReturnsDeterministicSortedNames()
    {
        var registry = new InMemoryProviderRegistry();

        registry.RegisterProvider("provider-z", new TestProvider());
        registry.RegisterProvider("provider-a", new TestProvider());

        var providers = registry.GetRegisteredProviders();

        Assert.Equal(["provider-a", "provider-z"], providers);
    }

    [Fact]
    public void Constructor_LoadsProviderSnapshot()
    {
        var registry = new InMemoryProviderRegistry(
        [
            new TestProvider("provider-z"),
            new TestProvider("provider-a")
        ]);

        Assert.Equal(["provider-a", "provider-z"], registry.GetRegisteredProviders());
    }

    [Fact]
    public void RegisterProvider_ReplacesExistingProvider()
    {
        var registry = new InMemoryProviderRegistry();
        var first = new TestProvider("provider");
        var second = new TestProvider("provider");

        registry.RegisterProvider("provider", first);
        registry.RegisterProvider("provider", second);

        Assert.Equal(["provider"], registry.GetRegisteredProviders());
    }

    [Fact]
    public void UnregisterProvider_ReturnsFalseForUnknownOrBlankName()
    {
        var registry = new InMemoryProviderRegistry();

        Assert.False(registry.UnregisterProvider("missing"));
        Assert.False(registry.UnregisterProvider(" "));
    }

    [Fact]
    public void DynamicProviderRegistryInterface_ExposesDynamicLoadSurface()
    {
        IDynamicProviderRegistry registry = new InMemoryProviderRegistry();

        registry.RegisterProvider(new TestProvider("dynamic-provider"));
        registry.RegisterInvoker(new TestInvoker());

        Assert.Equal(["dynamic-provider"], registry.GetRegisteredProviders());
        Assert.Single(registry.GetRegisteredInvokers());
    }

    [Fact]
    public void Constructor_LoadsProviderAndInvokerSnapshot()
    {
        var capabilityRegistry = new InMemoryCapabilityModuleRegistry();
        IDynamicProviderRegistry registry = new InMemoryProviderRegistry(
            capabilityRegistry,
            [new TestProvider("provider-a")],
            [new TestInvoker()]);

        Assert.Equal(["provider-a"], registry.GetRegisteredProviders());
        Assert.Single(registry.GetRegisteredInvokers());
    }

    [Fact]
    public async Task LoadProviderFromManifest_RegistersCapabilityMetadata()
    {
        var capabilityRegistry = new InMemoryCapabilityModuleRegistry();
        IDynamicProviderRegistry registry = new InMemoryProviderRegistry(capabilityRegistry);
        var path = Path.Combine(
            Path.GetTempPath(),
            $"aikernel-provider-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "providerId": "openai.chat",
              "name": "Chat OpenAI",
              "version": "0.1.0",
              "capabilities": ["chat.completion"],
              "metadata": {
                "endpoint": "https://api.openai.com/v1",
                "model": "gpt-4o"
              },
              "cli": {
                "command": "dynamic-pipeline",
                "defaultOperation": "chat.completion",
                "configKeys": ["model"],
                "requiredEnvironment": ["OPENAI_API_KEY"]
              }
            }
            """,
            TestContext.Current.CancellationToken);

        try
        {
            var loaded = await registry.LoadProviderFromManifest(
                path,
                TestContext.Current.CancellationToken);
            var descriptor = await capabilityRegistry.ResolveAsync(
                "openai.chat",
                TestContext.Current.CancellationToken);

            Assert.Empty(loaded);
            Assert.NotNull(descriptor);
            Assert.Equal("Chat OpenAI", descriptor.Name);
            Assert.Equal(["chat.completion"], descriptor.ProvidedOperations);
            Assert.Equal("dynamic-pipeline", descriptor.Metadata["cli.command"]);
            Assert.Equal("OPENAI_API_KEY", descriptor.Metadata["cli.env.OPENAI_API_KEY"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class TestProvider(string providerId = "test-provider") : IProvider
    {
        public string ProviderId => providerId;

        public string Name => "Test Provider";

        public string Version => "0.1.0";

        public IProviderCapabilities GetCapabilities()
        {
            return new TestProviderCapabilities();
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }

        public Task<ProviderHealthStatus> GetHealthAsync()
        {
            return Task.FromResult(new ProviderHealthStatus(
                IsHealthy: true,
                Message: "OK",
                CheckedAt: DateTime.UnixEpoch,
                ResponseTimeMs: 0));
        }
    }

    private sealed class TestProviderCapabilities : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => [];

        public IReadOnlyList<string> SupportedDataTypes => [];

        public int MaxConcurrentConnections => 1;

        public RateLimitInfo? RateLimit => null;

        public ModelCapacityVector Vector => new();

        public IDictionary<string, float>? GetDynamicCapacities(
            IExecutionConstraints constraints)
        {
            return null;
        }

        public ICapabilityProfile? GetCapabilityProfile()
        {
            return null;
        }

        public bool SupportsOperation(string operation)
        {
            return false;
        }

        public bool SupportsDataType(string dataType)
        {
            return false;
        }

        public bool SupportsQuantization(string quantizationLevel)
        {
            return false;
        }

        public bool SupportsQueryAugmentation => false;

        public bool SupportsQueryDecomposition => false;

        public bool SupportsQueryRouting => false;

        public int MaxQueryParts => 0;

        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        public bool SupportsQueryProcessingOperation(string operation)
        {
            return false;
        }

        public bool SupportsEmbedding => false;

        public int? EmbeddingDimensions => null;

        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }

    private sealed class TestInvoker : ICapabilityModuleInvoker
    {
        public ValueTask<CapabilityInvocationResult> InvokeAsync(
            CapabilityInvocationRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new CapabilityInvocationResult(
                request.InvocationId,
                request.CapabilityId,
                Succeeded: true,
                OutputHash: "test",
                ErrorCode: null,
                ErrorMessage: null,
                ReplayLogHash: request.ReplayLogHash,
                Metadata: request.Metadata));
        }
    }
}
