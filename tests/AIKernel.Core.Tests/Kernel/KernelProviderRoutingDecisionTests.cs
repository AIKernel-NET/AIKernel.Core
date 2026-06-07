namespace AIKernel.Core.Tests.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Rom;
using AIKernel.Dtos.Routing;
using AIKernel.Kernel;
using AIKernel.Vfs;

public sealed class KernelProviderRoutingDecisionTests
{
    [Fact]
    public void ForProvider_AddsProviderModelAndTierMetadata()
    {
        var decision = KernelProviderRoutingDecisionFactory.ForProvider(
            providerId: "llm-low",
            requestedModelId: "gpt-mini",
            providerTier: "low",
            routeReason: "short-context");

        var metadata = decision.ApplyTo(
            ImmutableDictionary<string, string>.Empty
                .Add("user_key", "user-value")
                .Add(KernelFacadeMetadataKeys.ProviderId, "user-provider"));

        Assert.Equal("llm-low", metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("gpt-mini", metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.Equal("low", metadata[KernelFacadeMetadataKeys.ProviderTier]);
        Assert.Equal("short-context", metadata[KernelFacadeMetadataKeys.RouteReason]);
        Assert.Equal("user-value", metadata["user_key"]);
    }

    [Fact]
    public void ForCapabilityModule_AddsCliCapabilityMetadata()
    {
        var decision = KernelProviderRoutingDecisionFactory.ForCapabilityModule(
            providerId: "tools-cli-adapter",
            requestedModelId: "aik-cli",
            capabilityModuleId: "AIKernel.Tools.Cli",
            routeReason: "aik-prefix");

        var metadata = decision.ToMetadata();

        Assert.Equal("tools-cli-adapter", metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("aik-cli", metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.Equal("capability", metadata[KernelFacadeMetadataKeys.ProviderTier]);
        Assert.Equal("AIKernel.Tools.Cli", metadata[KernelFacadeMetadataKeys.CapabilityModuleId]);
        Assert.Equal("aik-prefix", metadata[KernelFacadeMetadataKeys.RouteReason]);
    }

    [Fact]
    public void ApplyToRequest_UpdatesModelIdAndMetadata()
    {
        var request = CreateRequest("gpt-original");
        var decision = KernelProviderRoutingDecisionFactory.ForCapabilityModule(
            providerId: "tools-cli-adapter",
            requestedModelId: "aik-cli",
            capabilityModuleId: "AIKernel.Tools.Cli",
            routeReason: "aik-prefix",
            metadata: ImmutableDictionary<string, string>.Empty
                .Add("route_score", "1.0"));

        var routed = decision.ApplyToRequest(request);

        Assert.Equal("gpt-original", request.RequestedModelId);
        Assert.Equal("aik-cli", routed.RequestedModelId);
        Assert.Equal("tools-cli-adapter", routed.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("aik-cli", routed.Metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.Equal("AIKernel.Tools.Cli", routed.Metadata[KernelFacadeMetadataKeys.CapabilityModuleId]);
        Assert.Equal("1.0", routed.Metadata["route_score"]);
        Assert.Equal("user-value", routed.Metadata["user_key"]);
    }

    [Fact]
    public async Task ApplyToRequest_RoutesProviderAndPromptCapability()
    {
        var decision = KernelProviderRoutingDecisionFactory.ForCapabilityModule(
            providerId: "tools-cli-adapter",
            requestedModelId: "aik-cli",
            capabilityModuleId: "AIKernel.Tools.Cli",
            routeReason: "aik-prefix");
        var request = decision.ApplyToRequest(CreateRequest("gpt-original"));
        var expectedProvider = new FakeModelProvider("tools-cli-adapter");
        var providerSelector = new StaticKernelModelProviderSelector(
        [
            new FakeModelProvider("llm-low"),
            expectedProvider
        ]);
        var capabilityResolver = new StaticModelPromptCapabilityResolver(
        [
            CreateCapability("llm-low", "gpt-mini"),
            CreateCapability("tools-cli-adapter", "aik-cli")
        ]);

        var provider = await providerSelector.SelectAsync(
            request,
            CreateContextSnapshot(),
            TestContext.Current.CancellationToken);
        var capability = capabilityResolver.Resolve(
            provider,
            CreateExecutionRequest(request.RequestedModelId));

        Assert.Same(expectedProvider, provider);
        Assert.Equal("tools-cli-adapter", capability.ProviderId);
        Assert.Equal("aik-cli", capability.ModelId);
    }

    [Theory]
    [InlineData("aik://tools/run", "tools-cli-adapter", "aik-cli", "capability", "AIKernel.Tools.Cli")]
    [InlineData("short prompt", "llm-low", "gpt-mini", "low", null)]
    [InlineData("this is a long prompt that should use the stronger model", "llm-high", "gpt-frontier", "high", null)]
    public void ResultStepLinq_CanRouteUserlandProviderPipeline(
        string input,
        string expectedProviderId,
        string expectedModelId,
        string expectedTier,
        string? expectedCapabilityModuleId)
    {
        var routed =
            from normalized in ResultStep<string, string>
                .Success("routing:start", input)
                .Map(value => value.Trim())
                .WithSemanticDelta(new SemanticDelta("user.routing.normalize", OriginStep.KernelFacade, SemanticSlot.T))
            from decision in Route(normalized)
            select decision;

        Assert.True(routed.IsSuccess);

        var metadata = routed.Value!.ToMetadata();

        Assert.Equal(expectedProviderId, metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal(expectedModelId, metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.Equal(expectedTier, metadata[KernelFacadeMetadataKeys.ProviderTier]);

        if (expectedCapabilityModuleId is null)
        {
            Assert.False(metadata.ContainsKey(KernelFacadeMetadataKeys.CapabilityModuleId));
        }
        else
        {
            Assert.Equal(expectedCapabilityModuleId, metadata[KernelFacadeMetadataKeys.CapabilityModuleId]);
        }

        Assert.Equal(2, routed.ReplayLog.Count);
        Assert.StartsWith("replay:sha256:", routed.ReplayLogHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsBlankProviderId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => KernelProviderRoutingDecisionFactory.ForProvider("", "gpt-mini"));

        Assert.Equal("providerId is required. (Parameter 'providerId')", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsBlankRequestedModelId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => KernelProviderRoutingDecisionFactory.ForProvider("llm-low", ""));

        Assert.Equal("requestedModelId is required. (Parameter 'requestedModelId')", exception.Message);
    }

    private static ResultStep<string, KernelProviderRoutingDecision> Route(
        string normalized)
    {
        var decision = normalized.StartsWith("aik", StringComparison.OrdinalIgnoreCase)
            ? KernelProviderRoutingDecisionFactory.ForCapabilityModule(
                providerId: "tools-cli-adapter",
                requestedModelId: "aik-cli",
                capabilityModuleId: "AIKernel.Tools.Cli",
                routeReason: "aik-prefix")
            : normalized.Length < 32
                ? KernelProviderRoutingDecisionFactory.ForProvider(
                    providerId: "llm-low",
                    requestedModelId: "gpt-mini",
                    providerTier: "low",
                    routeReason: "short-context")
                : KernelProviderRoutingDecisionFactory.ForProvider(
                    providerId: "llm-high",
                    requestedModelId: "gpt-frontier",
                    providerTier: "high",
                    routeReason: "long-context");

        return ResultStep<string, KernelProviderRoutingDecision>
            .Success("routing:decision", decision)
            .WithSemanticDelta(new SemanticDelta("user.routing.provider", OriginStep.Capability, SemanticSlot.T));
    }

    private static KernelRequest CreateRequest(
        string requestedModelId)
    {
        return new KernelRequest
        {
            Input = "aik://tools/run",
            RootRomId = new RomId("rom://routing/test"),
            VfsProviderId = "memory-file",
            Credentials = new VfsCredentials(),
            Scope = new ContextAssemblyScope
            {
                Purpose = "routing-test",
                Capabilities = ["external"],
                Metadata = ImmutableDictionary<string, string>.Empty
            },
            PromptOptions = TestExecutionDefaults.PromptOptions,
            ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
            RequestedModelId = requestedModelId,
            Metadata = ImmutableDictionary<string, string>.Empty
                .Add("user_key", "user-value")
        };
    }

    private static IContextSnapshot CreateContextSnapshot()
    {
        return new AssembledContextSnapshot(
            snapshotId: "snapshot:routing",
            parentSnapshotId: null,
            createdAtUtc: DateTimeOffset.UnixEpoch,
            contextHash: "sha256:routing",
            context: new ContextCollectionSnapshot([]));
    }

    private static KernelExecutionRequest CreateExecutionRequest(
        string? requestedModelId)
    {
        return new KernelExecutionRequest
        {
            ContextSnapshotId = "snapshot:routing",
            ContextHash = "sha256:routing",
            ContextBlocks = [],
            UserInstruction = "aik://tools/run",
            PromptOptions = TestExecutionDefaults.PromptOptions,
            ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
            RequestedModelId = requestedModelId
        };
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

    private sealed class FakeModelProvider(
        string providerId) : IModelProvider
    {
        public string ProviderId { get; } = providerId;

        public string Name => "Fake Provider";

        public string Version => "0.1.0";

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
            return Task.FromResult("routing output");
        }

        public Task StreamGenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            Func<string, Task> onChunk,
            CancellationToken cancellationToken = default)
        {
            return onChunk("routing output");
        }

        public Task<string> AnswerAsync(
            string question,
            string? context = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("routing output");
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

    private sealed class TestVfsCredentials : IVfsCredentials
    {
        public string? Username => null;

        public string? ApiKey => null;

        public string? Token => null;

        public IReadOnlyDictionary<string, object> Parameters =>
            ImmutableDictionary<string, object>.Empty;
    }
}
