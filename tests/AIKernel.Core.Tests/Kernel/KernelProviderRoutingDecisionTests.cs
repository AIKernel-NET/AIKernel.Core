namespace AIKernel.Core.Tests.Kernel;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Kernel;

public sealed class KernelProviderRoutingDecisionTests
{
    [Fact]
    public void ForProvider_AddsProviderModelAndTierMetadata()
    {
        var decision = KernelProviderRoutingDecision.ForProvider(
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
        var decision = KernelProviderRoutingDecision.ForCapabilityModule(
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
            () => KernelProviderRoutingDecision.ForProvider("", "gpt-mini"));

        Assert.Equal("providerId is required. (Parameter 'providerId')", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsBlankRequestedModelId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => KernelProviderRoutingDecision.ForProvider("llm-low", ""));

        Assert.Equal("requestedModelId is required. (Parameter 'requestedModelId')", exception.Message);
    }

    private static ResultStep<string, KernelProviderRoutingDecision> Route(
        string normalized)
    {
        var decision = normalized.StartsWith("aik", StringComparison.OrdinalIgnoreCase)
            ? KernelProviderRoutingDecision.ForCapabilityModule(
                providerId: "tools-cli-adapter",
                requestedModelId: "aik-cli",
                capabilityModuleId: "AIKernel.Tools.Cli",
                routeReason: "aik-prefix")
            : normalized.Length < 32
                ? KernelProviderRoutingDecision.ForProvider(
                    providerId: "llm-low",
                    requestedModelId: "gpt-mini",
                    providerTier: "low",
                    routeReason: "short-context")
                : KernelProviderRoutingDecision.ForProvider(
                    providerId: "llm-high",
                    requestedModelId: "gpt-frontier",
                    providerTier: "high",
                    routeReason: "long-context");

        return ResultStep<string, KernelProviderRoutingDecision>
            .Success("routing:decision", decision)
            .WithSemanticDelta(new SemanticDelta("user.routing.provider", OriginStep.Capability, SemanticSlot.T));
    }
}
