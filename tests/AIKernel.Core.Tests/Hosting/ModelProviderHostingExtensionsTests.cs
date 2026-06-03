namespace AIKernel.Core.Tests.Hosting;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Routing;
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

public sealed class ModelProviderHostingExtensionsTests
{
    [Fact]
    public void WithModelProvider_RegistersExternalProviderAndCapability()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
                CreateCapability("external-capability", "rh-prime-phase"));

        using var provider = services.BuildServiceProvider();

        var modelProvider = Assert.Single(
            provider.GetServices<IModelProvider>(),
            x => x.ProviderId == "external-capability");
        var resolver = provider.GetRequiredService<IModelPromptCapabilityResolver>();

        var capability = resolver.Resolve(
            modelProvider,
            CreateExecutionRequest("rh-prime-phase"));

        Assert.Equal("external-capability", capability.ProviderId);
        Assert.Equal("rh-prime-phase", capability.ModelId);
    }

    [Fact]
    public void WithModelProvider_FactorySupportsExecutableAdapterProviders()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider(
                _ => new ExternalCapabilityProvider("tools-process-adapter"),
                _ => CreateCapability("tools-process-adapter", "tools-cli"));

        using var provider = services.BuildServiceProvider();

        var modelProvider = Assert.Single(
            provider.GetServices<IModelProvider>(),
            x => x.ProviderId == "tools-process-adapter");
        var capability = provider
            .GetRequiredService<IModelPromptCapabilityResolver>()
            .Resolve(modelProvider, CreateExecutionRequest("tools-cli"));

        Assert.Equal("tools-process-adapter", capability.ProviderId);
        Assert.Equal("tools-cli", capability.ModelId);
    }

    [Fact]
    public void WithModelProvider_RegistersMultipleCapabilitiesForOneProvider()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
            [
                CreateCapability("external-capability", "rh-prime-phase"),
                CreateCapability("external-capability", "rh-interference-energy")
            ]);

        using var provider = services.BuildServiceProvider();

        var modelProvider = Assert.Single(
            provider.GetServices<IModelProvider>(),
            x => x.ProviderId == "external-capability");
        var resolver = provider.GetRequiredService<IModelPromptCapabilityResolver>();

        var primePhase = resolver.Resolve(
            modelProvider,
            CreateExecutionRequest("rh-prime-phase"));
        var interferenceEnergy = resolver.Resolve(
            modelProvider,
            CreateExecutionRequest("rh-interference-energy"));

        Assert.Equal("rh-prime-phase", primePhase.ModelId);
        Assert.Equal("rh-interference-energy", interferenceEnergy.ModelId);
    }

    [Fact]
    public void WithModelProvider_RejectsInvalidCapabilityAtResolution()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
                new ModelPromptCapability
                {
                    ProviderId = "",
                    ModelId = "model"
                });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<ArgumentException>(
            () => provider.GetRequiredService<ModelPromptCapability>());

        Assert.Equal(
            "ModelPromptCapability.ProviderId is required. (Parameter 'capability')",
            exception.Message);
    }

    private static ModelPromptCapability CreateCapability(
        string providerId,
        string modelId)
    {
        return new ModelPromptCapability
        {
            ProviderId = providerId,
            ModelId = modelId,
            MessageFormat = PromptMessageFormat.ChatMessages,
            MaxInputTokens = 2048,
            MaxOutputTokens = 512,
            SupportedRoles = [ModelMessageRoles.User],
            SystemInstructionRole = ModelMessageRoles.User
        };
    }

    private static KernelExecutionRequest CreateExecutionRequest(
        string modelId)
    {
        return new KernelExecutionRequest
        {
            ContextSnapshot = CreateContextSnapshot(),
            UserInstruction = "run capability",
            PromptOptions = PromptGenerationOptions.Default,
            ExecutionOptions = ExecutionOptions.DeterministicDefault,
            RequestedModelId = modelId
        };
    }

    private static IContextSnapshot CreateContextSnapshot()
    {
        return new AssembledContextSnapshot(
            snapshotId: "snapshot:external-capability",
            parentSnapshotId: null,
            createdAtUtc: DateTimeOffset.UnixEpoch,
            contextHash: "sha256:external-capability",
            context: new ContextCollectionSnapshot([]));
    }

    private sealed class ExternalCapabilityProvider : IModelProvider
    {
        public ExternalCapabilityProvider()
            : this("external-capability")
        {
        }

        public ExternalCapabilityProvider(
            string providerId)
        {
            ProviderId = providerId;
        }

        public string ProviderId { get; }

        public string Name => "External Capability Provider";

        public string Version => "0.0.3";

        public IProviderCapabilities GetCapabilities()
        {
            return new ExternalProviderCapabilities();
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

        public Task<string> GenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("external output");
        }

        public Task StreamGenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            Func<string, Task> onChunk,
            CancellationToken cancellationToken = default)
        {
            return onChunk("external output");
        }

        public Task<string> AnswerAsync(
            string question,
            string? context = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("external output");
        }
    }

    private sealed class ExternalProviderCapabilities : IProviderCapabilities
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

        public bool SupportsOperation(
            string operation)
        {
            return false;
        }

        public bool SupportsDataType(
            string dataType)
        {
            return false;
        }

        public bool SupportsQuantization(
            string quantizationLevel)
        {
            return false;
        }

        public bool SupportsQueryAugmentation => false;

        public bool SupportsQueryDecomposition => false;

        public bool SupportsQueryRouting => false;

        public int MaxQueryParts => 0;

        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        public bool SupportsQueryProcessingOperation(
            string operation)
        {
            return false;
        }

        public bool SupportsEmbedding => false;

        public int? EmbeddingDimensions => null;

        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
