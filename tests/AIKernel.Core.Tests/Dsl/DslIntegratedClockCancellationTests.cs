namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Providers.LocalExecutionProvider;
using AIKernel.Core.Time;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Time;
using AIKernel.Enums;
using ContractDsl = AIKernel.Abstractions.Dsl;
using DslDtos = AIKernel.Dtos.Dsl;

public sealed class DslIntegratedClockCancellationTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
    private const string LoopJson = """
        {"type":"Pipeline","steps":[{"type":"LoopUntil","timeout":"00:00:30","maxIterations":3,
          "body":[{"type":"CallCapability","name":"work"}]}]}
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeAwaitingAnAsyncCapabilityCountsTowardsTheDeadlineAndReplays(bool rom)
    {
        var ticks = Start.UtcTicks;
        var recorded = new List<DateTimeOffset>();
        var clock = new TestClock(() =>
        {
            var now = new DateTimeOffset(Interlocked.Read(ref ticks), TimeSpan.Zero);
            recorded.Add(now);
            return now;
        });
        var entered = Signal();
        var release = Signal();
        var capabilities = new AsyncRegistry(async (input, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<DslPipelineValue>.Success(input.With("count", "1"));
        });
        var pending = new Fixture(rom, clock, capabilities).Invoke(TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.False(pending.IsCompleted);
            Interlocked.Exchange(ref ticks, Start.AddSeconds(30).UtcTicks);
        }
        finally { release.TrySetResult(); }
        var first = await pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, first.ErrorMessage); // Preserve the existing timeout result contract.
        Assert.Equal(1, capabilities.AsyncCalls);
        Assert.Equal(0, capabilities.SyncCalls);
        Assert.Equal(new[] { Start, Start, Start.AddSeconds(30) }, recorded);

        var times = new Queue<DateTimeOffset>(recorded);
        var replayCapabilities = new AsyncRegistry((input, _) =>
            Task.FromResult(Result<DslPipelineValue>.Success(input.With("count", "1"))));
        var replay = new Fixture(rom, new TestClock(() => times.Dequeue()), replayCapabilities);
        var second = await replay.Invoke(TestContext.Current.CancellationToken);
        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.Empty(times);
        Assert.Equal(1, replayCapabilities.AsyncCalls);
        Assert.Equal(0, replayCapabilities.SyncCalls);
        Assert.Equal(first.Metadata.OrderBy(pair => pair.Key), second.Metadata.OrderBy(pair => pair.Key));
        Assert.Equal(first.OutputHash, second.OutputHash);
        Assert.Equal(first.ReplayLogHash, second.ReplayLogHash);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TimedExecutionPropagatesCancellationSubtypesWithLinkedTokens(bool rom, bool taskCanceled)
    {
        using var parent = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(parent.Token);
        linked.Cancel();
        var clock = new TestClock(() => Start);
        var capabilities = new AsyncRegistry(async (_, _) =>
        {
            await Task.Yield();
            if (taskCanceled) throw new TaskCanceledException("Canceled", null, linked.Token);
            throw new DerivedCancellation(linked.Token);
        });
        var pending = new Fixture(rom, clock, capabilities).Invoke(parent.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(linked.Token, error.CancellationToken);
        Assert.True(pending.IsCanceled);
        Assert.False(parent.IsCancellationRequested);
        Assert.Equal(1, capabilities.AsyncCalls);
        Assert.Equal(0, capabilities.SyncCalls);
        Assert.Equal(2, clock.Reads); // Origin and the first iteration, with no second body.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringATimedAsyncBodyWaitsForItAndDiscardsItsLateValue(bool rom)
    {
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        var release = Signal();
        var clock = new TestClock(() => Start);
        var capabilities = new AsyncRegistry(async (input, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            entered.TrySetResult();
            await release.Task; // Deliberately non-cooperative: the execution must not detach it.
            return Result<DslPipelineValue>.Success(input.With("late", "must-not-escape"));
        });
        var pending = new Fixture(rom, clock, capabilities).Invoke(cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
        }
        finally { release.TrySetResult(); }
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.True(pending.IsCanceled);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, capabilities.AsyncCalls);
        Assert.Equal(0, capabilities.SyncCalls);
        Assert.Equal(2, clock.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationWhileReadingTheImplicitOriginIsNotConvertedToFailure(bool rom)
    {
        using var parent = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(parent.Token);
        linked.Cancel();
        var clock = new TestClock(() => throw new DerivedCancellation(linked.Token));
        var capabilities = new AsyncRegistry((_, _) => throw new InvalidOperationException("Body must not run."));
        var pending = new Fixture(rom, clock, capabilities).Invoke(parent.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(pending.IsCanceled);
        Assert.Equal(linked.Token, error.CancellationToken);
        Assert.False(parent.IsCancellationRequested);
        Assert.Equal(0, capabilities.AsyncCalls);
        Assert.Equal(0, capabilities.SyncCalls);
        Assert.Equal(1, clock.Reads);
    }

    [Fact]
    public async Task PublicAsyncExecutionKeepsAnExplicitExpiredOriginAndItsReplayHash()
    {
        var clock = new TestClock(() => Start.AddSeconds(30));
        var capabilities = new AsyncRegistry((_, _) => throw new InvalidOperationException("Body must not run."));
        var document = DslDocument.FromJson(LoopJson);
        Assert.True(document.IsSuccess);
        var compiled = new DslPipelineCompiler(capabilities, clock).Compile(document.Value!);
        Assert.True(compiled.IsSuccess);
        var synchronous = compiled.Value!.Execute(new(DslPipelineValue.Empty, Start));
        var asynchronous = await ((ContractDsl.IKernelPipeline)compiled.Value).ExecuteAsync(
            new DslDtos.DslPipelineExecutionContext(new DslDtos.DslPipelineValue(new Dictionary<string, string>()), Start),
            TestContext.Current.CancellationToken);
        Assert.True(synchronous.IsSuccess);
        Assert.Equal(ExecutionStatus.Succeeded, asynchronous.Status);
        Assert.Equal("timeout_reached", Assert.Single(synchronous.ReplayLog).SemanticDelta.Metadata![PipelineStepMetadataKeys.LoopDecision]);
        Assert.Equal(synchronous.ReplayLogHash, asynchronous.ReplayLogHash);
        Assert.Equal(synchronous.ReplayLog.Count, asynchronous.ReplayLogCount);
        Assert.Equal(0, capabilities.AsyncCalls);
        Assert.Equal(0, capabilities.SyncCalls);
        Assert.Equal(2, clock.Reads);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Fixture
    {
        private readonly LocalExecutionInvoker _invoker;
        private readonly string _json;

        internal Fixture(bool rom, IKernelClock clock, IDslCapabilityRegistry capabilities)
        {
            var roms = new DslRomRegistry();
            var registry = new DslRomCapabilityRegistry(capabilities, roms);
            _json = LoopJson;
            if (rom)
            {
                var provider = new DslRomProvider(new DslPipelineCompiler(registry, clock));
                var snapshot = provider.CreateSnapshot("integration", "timed", LoopJson, DateTimeOffset.UnixEpoch);
                Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
                Assert.True(roms.Register(snapshot.Value!).IsSuccess);
                _json = """{"type":"Pipeline","steps":[{"type":"CallCapability","name":"dsl://integration/timed"}]}""";
                // The untimed caller must not replace the ROM's own clock with a host clock.
                clock = new TestClock(() => throw new InvalidOperationException("Untimed caller clock read."));
            }
            _invoker = new LocalExecutionInvoker(new DslPipelineCompiler(registry, clock));
        }

        internal Task<CapabilityInvocationResult> Invoke(CancellationToken token) => _invoker.InvokeAsync(
            new("integration", "aikernel.local.execute", "pipeline.execute",
                new Dictionary<string, string> { ["pipeline.json"] = _json }, null, null, new Dictionary<string, string>()), token).AsTask();
    }

    private sealed class AsyncRegistry(Func<DslPipelineValue, CancellationToken, Task<Result<DslPipelineValue>>> invoke)
        : IDslCapabilityRegistry
    {
        internal int SyncCalls;
        internal int AsyncCalls;
        public bool Contains(string name) => name == "work";
        public Result<DslPipelineValue> Invoke(string name, DslPipelineValue input, IReadOnlyDictionary<string, string> args)
        {
            Interlocked.Increment(ref SyncCalls);
            throw new InvalidOperationException("Synchronous capability path used.");
        }
        public Task<Result<DslPipelineValue>> InvokeAsync(string name, DslPipelineValue input,
            IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref AsyncCalls);
            return invoke(input, cancellationToken);
        }
    }

    private sealed class DerivedCancellation(CancellationToken token) : OperationCanceledException(token);

    private sealed class TestClock(Func<DateTimeOffset> read) : IKernelClock
    {
        internal int Reads;
        public TimeProvider Physical => TimeProvider.System;
        public KernelTimeProvider Logical { get; } = new ReplayKernelTimeProvider(Start, TimeProvider.System);
        public DateTimeOffset Now { get { Interlocked.Increment(ref Reads); return read(); } }
        public bool IsReplaying => true;
        public double ReliabilityScore => 1;
        public KernelTimestamp GetLogicalTimestamp() => new() { UtcDateTime = Now, SourceId = "integration" };
    }
}
