namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Providers.LocalExecutionProvider;
using AIKernel.Core.Time;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Time;

public sealed class LoopUntilClockTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemClockRunsThreeBodiesAndRecordedClockReplaysIdentically(bool rom)
    {
        var system = KernelClock.System();
        var recorded = new List<DateTimeOffset>();
        var clock = new TestClock(() => { var now = system.Now; recorded.Add(now); return now; });
        var live = new Fixture(rom, clock, Loop());
        var first = await live.Invoke();
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal(3, live.Capabilities.Calls);
        Assert.Equal(4, recorded.Count); // One origin and three iteration checks.

        var times = new Queue<DateTimeOffset>(recorded);
        var replay = new Fixture(rom, new TestClock(() => times.Dequeue()), Loop());
        var second = await replay.Invoke();
        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.Equal(3, replay.Capabilities.Calls);
        Assert.Empty(times);
        Assert.Equal(first.Metadata.OrderBy(x => x.Key), second.Metadata.OrderBy(x => x.Key));
        Assert.Equal(first.OutputHash, second.OutputHash);
        Assert.Equal(first.ReplayLogHash, second.ReplayLogHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdvancingClockStopsExactlyAtDeadlineAndReplays(bool rom)
    {
        var sequence = new[] { Start, Start, Start.AddSeconds(29), Start.AddSeconds(30) };
        async Task<CapabilityInvocationResult> Run()
        {
            var times = new Queue<DateTimeOffset>(sequence);
            var fixture = new Fixture(rom, new TestClock(() => times.Dequeue()), Loop());
            var result = await fixture.Invoke();
            Assert.True(result.Succeeded, result.ErrorMessage); // Existing timeout status is unchanged.
            Assert.Equal(2, fixture.Capabilities.Calls);
            Assert.Empty(times);
            return result;
        }
        var first = await Run(); var second = await Run();
        Assert.Equal(first.OutputHash, second.OutputHash);
        Assert.Equal(first.ReplayLogHash, second.ReplayLogHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachInvocationGetsItsOwnClockOrigin(bool rom)
    {
        var now = Start; var clock = new TestClock(() => now);
        var fixture = new Fixture(rom, clock, Loop());
        Assert.True((await fixture.Invoke()).Succeeded);
        now = now.AddHours(1);
        Assert.True((await fixture.Invoke()).Succeeded);
        Assert.Equal(6, fixture.Capabilities.Calls);
        Assert.Equal(8, clock.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroTimeoutStillRunsNoBody(bool rom)
    {
        var fixture = new Fixture(rom, new TestClock(() => Start), Loop(timeout: "00:00:00"));
        Assert.True((await fixture.Invoke()).Succeeded);
        Assert.Equal(0, fixture.Capabilities.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroIterationsDoesNotReadTheClock(bool rom)
    {
        var clock = new TestClock(() => throw new InvalidOperationException("clock unavailable"));
        var fixture = new Fixture(rom, clock, Loop(iterations: 0));
        Assert.True((await fixture.Invoke()).Succeeded);
        Assert.Equal(0, fixture.Capabilities.Calls);
        Assert.Equal(0, clock.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UntimedPipelineDoesNotReadTheClock(bool rom)
    {
        var clock = new TestClock(() => throw new InvalidOperationException("clock unavailable"));
        var fixture = new Fixture(rom, clock, """{"type":"Pipeline","steps":[{"type":"CallCapability","name":"Count"}]}""");
        Assert.True((await fixture.Invoke()).Succeeded);
        Assert.Equal(1, fixture.Capabilities.Calls);
        Assert.Equal(0, clock.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginClockFailureCannotExecuteTheBody(bool rom)
    {
        var fixture = new Fixture(rom, new TestClock(() => throw new InvalidOperationException("clock unavailable")), Loop());
        Assert.False((await fixture.Invoke()).Succeeded);
        Assert.Equal(0, fixture.Capabilities.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeSpentBeforeTheLoopCountsTowardsItsDeadline(bool rom)
    {
        var now = Start;
        var clock = new TestClock(() => now);
        var fixture = new Fixture(rom, clock, Loop(prefix: """{"type":"CallCapability","name":"Count"},"""));
        fixture.Capabilities.OnCall = () => now = now.AddSeconds(30);
        Assert.True((await fixture.Invoke()).Succeeded);
        Assert.Equal(1, fixture.Capabilities.Calls);
        Assert.Equal(2, clock.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedLoopsShareThePipelineOrigin(bool rom)
    {
        var now = Start; var clock = new TestClock(() => now);
        var fixture = new Fixture(rom, clock, """
            {"type":"Pipeline","steps":[{"type":"Loop","maxIterations":3,"body":[
              {"type":"LoopUntil","timeout":"00:00:30","maxIterations":3,"body":[
                {"type":"CallCapability","name":"Count"}]}]}]}
            """);
        fixture.Capabilities.OnCall = () => now = now.AddSeconds(10);
        Assert.True((await fixture.Invoke()).Succeeded);
        Assert.Equal(3, fixture.Capabilities.Calls);
        Assert.Equal(6, clock.Reads); // Origin + 3 checks + two expired inner loops.
    }

    [Fact]
    public void ExplicitContextStartIsNeverRebased()
    {
        var registry = new CountingRegistry();
        var clock = new TestClock(() => Start.AddSeconds(30));
        var pipeline = Compile(Loop(), registry, clock);
        var result = pipeline.Execute(new(DslPipelineValue.Empty, Start));
        Assert.True(result.IsSuccess);
        Assert.Equal(0, registry.Calls);
        Assert.Equal(1, clock.Reads);
        Assert.Equal("timeout_reached", Assert.Single(result.ReplayLog).SemanticDelta.Metadata![PipelineStepMetadataKeys.LoopDecision]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EpochReplayKeepsTheExistingDirectExecutionHash(bool rom)
    {
        var fixture = new Fixture(rom, KernelClock.Replay(DateTimeOffset.UnixEpoch), Loop());
        var result = await fixture.Invoke();
        var direct = Compile(Loop(), new CountingRegistry(), KernelClock.Replay(DateTimeOffset.UnixEpoch))
            .Execute(DslPipelineExecutionContext.Create());
        Assert.True(result.Succeeded); Assert.True(direct.IsSuccess);
        Assert.Equal(3, fixture.Capabilities.Calls);
        Assert.Equal(direct.ReplayLogHash, rom ? result.Metadata["output." + DslRomMetadataKeys.RomReplayLogHash] : result.ReplayLogHash);
    }

    private static string Loop(string timeout = "00:00:30", int iterations = 3, string prefix = "") => $$"""
        {"type":"Pipeline","steps":[{{prefix}}{"type":"LoopUntil","timeout":"{{timeout}}","maxIterations":{{iterations}},
          "body":[{"type":"CallCapability","name":"Count"}]}]}
        """;

    private static IKernelPipeline Compile(string json, IDslCapabilityRegistry registry, IKernelClock clock)
    {
        var parsed = DslDocument.FromJson(json); Assert.True(parsed.IsSuccess, parsed.Error?.Message);
        var compiled = new DslPipelineCompiler(registry, clock).Compile(parsed.Value!);
        Assert.True(compiled.IsSuccess, compiled.Error?.Message); return compiled.Value!;
    }

    private sealed class Fixture
    {
        internal CountingRegistry Capabilities { get; } = new();
        private readonly LocalExecutionInvoker _invoker;
        private readonly string _json;
        internal Fixture(bool rom, IKernelClock clock, string json)
        {
            var roms = new DslRomRegistry();
            var registry = new DslRomCapabilityRegistry(Capabilities, roms);
            _invoker = new(new DslPipelineCompiler(registry, clock));
            if (rom)
            {
                var metadata = new DslRomMetadata("test", "loop", "rom/dsl/test/loop.json", "dsl://test/loop", DslRomHasher.ComputeHash(json), DateTimeOffset.UnixEpoch);
                Assert.True(roms.Register(new(metadata, json, Compile(json, registry, clock))).IsSuccess);
                _json = """{"type":"Pipeline","steps":[{"type":"CallCapability","name":"dsl://test/loop"}]}""";
            }
            else _json = json;
        }
        internal ValueTask<CapabilityInvocationResult> Invoke() => _invoker.InvokeAsync(new("b1", "aikernel.local.execute", "pipeline.execute",
            new Dictionary<string, string> { ["pipeline.json"] = _json }, null, null, new Dictionary<string, string>()), TestContext.Current.CancellationToken);
    }

    private sealed class CountingRegistry : IDslCapabilityRegistry
    {
        internal int Calls;
        internal Action? OnCall;
        public bool Contains(string name) => name == "Count";
        public Result<DslPipelineValue> Invoke(string name, DslPipelineValue input, IReadOnlyDictionary<string, string> args)
        {
            Calls++; OnCall?.Invoke();
            return Result<DslPipelineValue>.Success(input.With("count", Calls.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private sealed class TestClock(Func<DateTimeOffset> read) : IKernelClock
    {
        internal int Reads;
        public TimeProvider Physical => TimeProvider.System;
        public KernelTimeProvider Logical { get; } = new ReplayKernelTimeProvider(Start, TimeProvider.System);
        public DateTimeOffset Now { get { Reads++; return read(); } }
        public bool IsReplaying => true;
        public double ReliabilityScore => 1;
        public KernelTimestamp GetLogicalTimestamp() => new() { UtcDateTime = Now, SourceId = "b1" };
    }
}
