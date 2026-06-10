using AIKernel.Core.Control;
using AIKernel.Abstractions.Events;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

namespace AIKernel.Core.Tests;

public sealed class BonsaiEngineTests
{
    [Fact]
    public async Task BonsaiEngine_EvaluatesRegisteredRule()
    {
        var engine = new BonsaiEngine();
        engine.RegisterRule(new ContainsRule("metric-low", "metric low", "dispatch-action"));

        var result = await engine.EvaluateAsync("process: metric low", TestContext.Current.CancellationToken);

        Assert.True(result.Matched);
        Assert.Equal("metric-low", result.RuleId);
        Assert.Equal("dispatch-action", result.Output);
    }

    [Fact]
    public async Task BonsaiEngine_ReturnsInputWhenNoRuleMatches()
    {
        var engine = new BonsaiEngine();

        var result = await engine.EvaluateAsync("process: stable", TestContext.Current.CancellationToken);

        Assert.False(result.Matched);
        Assert.Null(result.RuleId);
        Assert.Equal("process: stable", result.Output);
    }

    [Fact]
    public async Task BonsaiEngine_PublishesMatchedAndMissedEvents()
    {
        var eventBus = new CapturingEventBus();
        var engine = new BonsaiEngine(new RuleEvaluator(), eventBus);
        engine.RegisterRule(new ContainsRule("metric-low", "metric low", "dispatch-action"));

        await engine.EvaluateAsync("process: metric low", TestContext.Current.CancellationToken);
        await engine.EvaluateAsync("process: stable", TestContext.Current.CancellationToken);

        Assert.Contains(eventBus.Events, item => item.EventName == "BonsaiRuleMatched");
        Assert.Contains(eventBus.Events, item => item.EventName == "BonsaiRuleMissed");
    }

    private sealed class ContainsRule(
        string ruleId,
        string needle,
        string output) : IBonsaiRule
    {
        public string RuleId { get; } = ruleId;

        public bool Matches(string input)
            => input.Contains(needle, StringComparison.OrdinalIgnoreCase);

        public string Execute(string input) => output;
    }

    private sealed class CapturingEventBus : IEventBus
    {
        public List<CapturedEvent> Events { get; } = [];

        public string ProviderId => "test.eventbus";

        public string Name => "Test EventBus";

        public string Version => "0.1.0";

        public IProviderCapabilities GetCapabilities() => new TestProviderCapabilities();

        public Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public Task InitializeAsync() => Task.CompletedTask;

        public Task ShutdownAsync() => Task.CompletedTask;

        public Task<ProviderHealthStatus> GetHealthAsync()
            => Task.FromResult(new ProviderHealthStatus(true, "ok", DateTime.UtcNow, 0));

        public Task PublishAsync(string eventName, object eventData, CancellationToken cancellationToken = default)
        {
            Events.Add(new CapturedEvent(eventName, eventData));
            return Task.CompletedTask;
        }

        public Task BroadcastAsync(string eventName, object eventData, CancellationToken cancellationToken = default)
            => PublishAsync(eventName, eventData, cancellationToken);

        public string Subscribe<T>(string eventName, Func<T, Task> handler) => Guid.NewGuid().ToString("N");

        public bool Unsubscribe(string subscriptionId) => true;

        public int GetSubscriberCount(string eventName) => 0;
    }

    private sealed record CapturedEvent(string EventName, object Payload);

    private sealed class TestProviderCapabilities : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => [];

        public IReadOnlyList<string> SupportedDataTypes => [];

        public int MaxConcurrentConnections => 1;

        public RateLimitInfo? RateLimit => null;

        public ModelCapacityVector Vector => new();

        public IDictionary<string, float>? GetDynamicCapacities(AIKernel.Abstractions.Models.IExecutionConstraints constraints) => null;

        public AIKernel.Abstractions.Models.ICapabilityProfile? GetCapabilityProfile() => null;

        public bool SupportsOperation(string operation) => false;

        public bool SupportsDataType(string dataType) => false;

        public bool SupportsQuantization(string quantizationLevel) => false;

        public bool SupportsQueryAugmentation => false;

        public bool SupportsQueryDecomposition => false;

        public bool SupportsQueryRouting => false;

        public int MaxQueryParts => 0;

        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        public bool SupportsQueryProcessingOperation(string operation) => false;

        public bool SupportsEmbedding => false;

        public int? EmbeddingDimensions => null;

        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
