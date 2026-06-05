namespace AIKernel.Core.Tests.Providers;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Providers;
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
    public void RegisterProvider_ReplacesExistingProvider()
    {
        var registry = new InMemoryProviderRegistry();
        var first = new TestProvider();
        var second = new TestProvider();

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

    private sealed class TestProvider : IProvider
    {
        public string ProviderId => "test-provider";

        public string Name => "Test Provider";

        public string Version => "0.0.4";

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
}
