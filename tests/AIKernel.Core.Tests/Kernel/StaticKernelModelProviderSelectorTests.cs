namespace AIKernel.Core.Tests.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Context;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Rom;
using AIKernel.Dtos.Routing;
using AIKernel.Kernel;
using AIKernel.Vfs;

public sealed class StaticKernelModelProviderSelectorTests
{
    [Fact]
    public async Task SelectAsync_UsesPublicProviderIdMetadataKey()
    {
        var expected = new FakeModelProvider("external-capability");
        var selector = new StaticKernelModelProviderSelector(
        [
            new FakeModelProvider("openai-compatible"),
            expected
        ]);

        var provider = await selector.SelectAsync(
            CreateRequest(ImmutableDictionary<string, string>.Empty
                .Add(KernelFacadeMetadataKeys.ProviderId, "external-capability")),
            CreateContextSnapshot(),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, provider);
    }

    [Fact]
    public async Task SelectAsync_RejectsMissingProviderIdMetadata()
    {
        var selector = new StaticKernelModelProviderSelector(
        [
            new FakeModelProvider("external-capability")
        ]);

        var exception = await Assert.ThrowsAsync<KernelRequestValidationException>(
            () => selector.SelectAsync(
                CreateRequest(ImmutableDictionary<string, string>.Empty),
                CreateContextSnapshot(),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            "provider_id metadata is required for static provider selection.",
            exception.Message);
    }

    [Fact]
    public void KernelFacadeMetadataKeys_ExposeProviderSelectionKey()
    {
        Assert.Equal("provider_id", KernelFacadeMetadataKeys.ProviderId);
    }

    [Fact]
    public void Constructor_RejectsDuplicateProviderIds()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new StaticKernelModelProviderSelector(
            [
                new FakeModelProvider("external-capability"),
                new FakeModelProvider("external-capability")
            ]));

        Assert.Equal(
            "Duplicate model provider registration. ProviderId='external-capability'. (Parameter 'providers')",
            exception.Message);
    }

    [Fact]
    public void Constructor_RejectsBlankProviderId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new StaticKernelModelProviderSelector(
            [
                new FakeModelProvider("")
            ]));

        Assert.Equal(
            "IModelProvider.ProviderId is required. (Parameter 'provider')",
            exception.Message);
    }

    private static KernelRequest CreateRequest(
        ImmutableDictionary<string, string> metadata)
    {
        return new KernelRequest
        {
            Input = "run external capability",
            RootRomId = new RomId("rom://external/capability"),
            VfsProviderId = "memory-file",
            Credentials = new VfsCredentials(),
            Scope = new ContextAssemblyScope
            {
                Purpose = "external-capability-test",
                Capabilities = ["external"],
                Metadata = ImmutableDictionary<string, string>.Empty
            },
            PromptOptions = TestExecutionDefaults.PromptOptions,
            ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
            RequestedModelId = "gpt-test",
            Metadata = metadata
        };
    }

    private static IContextSnapshot CreateContextSnapshot()
    {
        return new AssembledContextSnapshot(
            snapshotId: "snapshot:selector",
            parentSnapshotId: null,
            createdAtUtc: DateTimeOffset.UnixEpoch,
            contextHash: "sha256:selector",
            context: new ContextCollectionSnapshot([]));
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
            return Task.FromResult("contract output");
        }

        public Task StreamGenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            Func<string, Task> onChunk,
            CancellationToken cancellationToken = default)
        {
            return onChunk("contract output");
        }

        public Task<string> AnswerAsync(
            string question,
            string? context = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("contract output");
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

    private sealed class FakeVfsCredentials : IVfsCredentials
    {
        public string? Username => null;

        public string? ApiKey => null;

        public string? Token => null;

        public IReadOnlyDictionary<string, object> Parameters =>
            ImmutableDictionary<string, object>.Empty;
    }
}
