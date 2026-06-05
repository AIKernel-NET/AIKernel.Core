namespace AIKernel.Core.Tests.Execution;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Routing;

public sealed class StaticModelPromptCapabilityResolverTests
{
    [Fact]
    public void Resolve_ReturnsCapabilityForProviderAndModel()
    {
        var resolver = new StaticModelPromptCapabilityResolver(
        [
            CreateCapability("external-capability", "rh-prime-phase")
        ]);

        var capability = resolver.Resolve(
            new FakeModelProvider("external-capability"),
            CreateExecutionRequest("rh-prime-phase"));

        Assert.Equal("external-capability", capability.ProviderId);
        Assert.Equal("rh-prime-phase", capability.ModelId);
    }

    [Fact]
    public void Constructor_RejectsDuplicateProviderModelPair()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new StaticModelPromptCapabilityResolver(
            [
                CreateCapability("external-capability", "rh-prime-phase"),
                CreateCapability("external-capability", "rh-prime-phase")
            ]));

        Assert.Equal(
            "Duplicate prompt capability registration. ProviderId='external-capability', ModelId='rh-prime-phase'. (Parameter 'capabilities')",
            exception.Message);
    }

    [Fact]
    public void Constructor_RejectsBlankProviderId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new StaticModelPromptCapabilityResolver(
            [
                CreateCapability("", "rh-prime-phase")
            ]));

        Assert.Equal(
            "ModelPromptCapability.ProviderId is required. (Parameter 'capability')",
            exception.Message);
    }

    [Fact]
    public void Constructor_RejectsBlankModelId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new StaticModelPromptCapabilityResolver(
            [
                CreateCapability("external-capability", "")
            ]));

        Assert.Equal(
            "ModelPromptCapability.ModelId is required. (Parameter 'capability')",
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
            SupportedRoles = ["user"],
            SystemInstructionRole = "user"
        };
    }

    private static KernelExecutionRequest CreateExecutionRequest(
        string modelId)
    {
        return new KernelExecutionRequest
        {
            ContextSnapshotId = "snapshot:capability",
            ContextHash = "sha256:capability",
            ContextBlocks = [],
            UserInstruction = "run capability",
            PromptOptions = TestExecutionDefaults.PromptOptions,
            ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
            RequestedModelId = modelId
        };
    }

    private sealed class FakeModelProvider(
        string providerId) : IModelProvider
    {
        public string ProviderId { get; } = providerId;

        public string Name => "Fake Provider";

        public string Version => "0.0.4";

        public IProviderCapabilities GetCapabilities()
        {
            return new FakeProviderCapabilities();
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

    private sealed class FakeProviderCapabilities : IProviderCapabilities
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
