namespace AIKernel.Core.Tests.Routing;

using AIKernel.Core.Routing;
using AIKernel.Dtos.Routing;
using AIKernel.Dtos.Rules;
using Xunit;

public sealed class InMemoryCapabilityRegistryTests
{
    [Fact]
    public async Task ResolveCandidatesAsync_ReturnsDeterministicSortedProviderIds()
    {
        var registry = new InMemoryCapabilityRegistry();

        await registry.RegisterCapabilityAsync(
            "provider-z",
            CreateCapacity(),
            TestContext.Current.CancellationToken);
        await registry.RegisterCapabilityAsync(
            "provider-a",
            CreateCapacity(),
            TestContext.Current.CancellationToken);

        var candidates = await registry.ResolveCandidatesAsync(
            new RuleEvaluationContext("context", "phase", new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.Equal(["provider-a", "provider-z"], candidates);
    }

    [Fact]
    public async Task Constructor_LoadsCapabilitySnapshot()
    {
        var registry = new InMemoryCapabilityRegistry(
        [
            CreateCapability("provider-z"),
            CreateCapability("provider-a")
        ]);

        var candidates = await registry.ResolveCandidatesAsync(
            new RuleEvaluationContext("context", "phase", new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.Equal(["provider-a", "provider-z"], candidates);
    }

    [Fact]
    public async Task GetCapabilityAsync_ReturnsRegisteredVector()
    {
        var registry = new InMemoryCapabilityRegistry();
        var capacity = CreateCapacity();

        await registry.RegisterCapabilityAsync(
            "provider",
            capacity,
            TestContext.Current.CancellationToken);

        var actual = await registry.GetCapabilityAsync(
            "provider",
            TestContext.Current.CancellationToken);

        Assert.Same(capacity, actual);
    }

    [Fact]
    public async Task GetCapabilityAsync_ReturnsNullForUnknownProvider()
    {
        var registry = new InMemoryCapabilityRegistry();

        var result = await registry.GetCapabilityAsync(
            "missing",
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static ModelCapacityVector CreateCapacity()
    {
        return new ModelCapacityVector(
            structuralIntegrity: 1,
            linguisticFluidity: 1,
            reasoningDepth: 1,
            fidelity: 1,
            latencyPerformance: 1);
    }

    private static AIKernel.Dtos.Execution.ModelPromptCapability CreateCapability(
        string providerId)
    {
        return new AIKernel.Dtos.Execution.ModelPromptCapability
        {
            ProviderId = providerId,
            ModelId = "model",
            MaxInputTokens = 10,
            MaxOutputTokens = 10,
            SupportedRoles = ["user"],
            SystemInstructionRole = "user"
        };
    }
}
