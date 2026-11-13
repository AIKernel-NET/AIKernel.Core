namespace AIKernel.Core.Tests.Dsl;

using System.Text.Json;
using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Time;
using AIKernel.Dtos.Time;
using AIKernel.Enums;
using ContractDsl = AIKernel.Abstractions.Dsl;

public sealed partial class DslAsyncExecutionTests
{
    [Theory]
    [InlineData("step")]
    [InlineData("nested")]
    [InlineData("zero-loop")]
    [InlineData("zero-until")]
    [InlineData("timeout")]
    [InlineData("clock-error")]
    [InlineData("failure")]
    [InlineData("exception")]
    [InlineData("null-value")]
    [InlineData("null-data")]
    [InlineData("bad-key")]
    [InlineData("bad-value")]
    [InlineData("suspend")]
    [InlineData("invalid-loop")]
    [InlineData("invalid-until")]
    [InlineData("invalid-context")]
    [InlineData("invalid-input")]
    public async Task AsyncAndSynchronousExecutionPreserveValuesFailuresAndEveryReplayEntry(string scenario)
    {
        var registry = new SyncRegistry((_, input, _) => scenario switch
        {
            "failure" => Result<DslPipelineValue>.Fail(new ErrorContext("Expected failure", "PF3_TEST_FAILURE", false)),
            "exception" => throw new InvalidOperationException("Expected capability exception"),
            "null-value" => Result<DslPipelineValue>.Success(null!),
            "null-data" => Result<DslPipelineValue>.Success(new DslPipelineValue(null!)),
            "bad-key" => Result<DslPipelineValue>.Success(new DslPipelineValue(new Dictionary<string, string> { [""] = "bad" })),
            "bad-value" => Result<DslPipelineValue>.Success(new DslPipelineValue(new Dictionary<string, string> { ["key"] = null! })),
            _ => Result<DslPipelineValue>.Success(input.With("answer", "42"))
        });
        PipelineNode node = scenario switch
        {
            "step" => new StepNode("hello"),
            "nested" => new LoopNode(2, [new StepNode("start"), new LoopUntilNode(TimeSpan.FromSeconds(30), 2, [Call()])]),
            "zero-loop" => new LoopNode(0, [Call()]),
            "zero-until" => new LoopUntilNode(TimeSpan.FromSeconds(30), 0, [Call()]),
            "timeout" => new LoopUntilNode(TimeSpan.Zero, 3, [Call()]),
            "clock-error" => new LoopUntilNode(TimeSpan.FromSeconds(30), 3, [Call()]),
            "suspend" => new LoopNode(3, [new SuspendNode("pause"), Call()]),
            "invalid-loop" => new LoopNode(-1, [Call()]),
            "invalid-until" => new LoopUntilNode(TimeSpan.FromSeconds(-1), 1, [Call()]),
            _ => new LoopNode(3, [Call(), new StepNode("after")])
        };
        var pipeline = Pipeline(node, registry,
            scenario == "clock-error" ? new TestClock(() => throw new InvalidOperationException("Expected clock failure")) : null);
        var context = scenario switch
        {
            "invalid-context" => null!,
            "invalid-input" => new DslPipelineExecutionContext(null!, DateTimeOffset.UnixEpoch),
            _ => Context()
        };
        var synchronous = pipeline.Execute(context);
        var asynchronous = await pipeline.ExecuteAsync(context, TestContext.Current.CancellationToken);
        AssertEquivalent(synchronous, asynchronous);

        if (scenario is not ("invalid-context" or "invalid-input"))
        {
            var projection = await ((ContractDsl.IKernelPipeline)pipeline).ExecuteAsync(
                DslContractMapper.ToCore(context), TestContext.Current.CancellationToken);
            Assert.Equal(synchronous.IsSuccess ? ExecutionStatus.Succeeded : ExecutionStatus.Failed, projection.Status);
            Assert.Equal(synchronous.ReplayLogHash, projection.ReplayLogHash);
            Assert.Equal(synchronous.ReplayLog.Count, projection.ReplayLogCount);
            Assert.Equal(synchronous.Error?.Code, projection.Error?.Code);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedRomExecutionPreservesRomMetadataAndReplayHashes(bool fail)
    {
        var inner = new SyncRegistry((_, input, _) => fail
            ? Result<DslPipelineValue>.Fail(new ErrorContext("Expected failure", "PF3_TEST_FAILURE", false))
            : Result<DslPipelineValue>.Success(input.With("answer", "42")));
        var registry = NestedRomRegistry(inner);
        var pipeline = Pipeline(new CallCapabilityNode("dsl://pf3/outer", Args), registry);
        var synchronous = pipeline.Execute(Context());
        var asynchronous = await pipeline.ExecuteAsync(Context(), TestContext.Current.CancellationToken);
        AssertEquivalent(synchronous, asynchronous);
        var metadata = fail ? asynchronous.Error!.Metadata! : asynchronous.Value!.Data;
        Assert.Equal("dsl://pf3/outer", metadata[DslRomMetadataKeys.RomCall]);
        Assert.False(string.IsNullOrWhiteSpace(metadata[DslRomMetadataKeys.RomReplayLogHash]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedRomCancellationReachesTheAsyncCapability(bool directRegistry)
    {
        using var cancellation = new CancellationTokenSource();
        var started = Signal();
        var inner = new AsyncRegistry(async (input, _, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result<DslPipelineValue>.Success(input);
        });
        var registry = NestedRomRegistry(inner);
        Task pending = directRegistry
            ? ((ContractDsl.IDslCapabilityRegistry)registry).InvokeAsync("dsl://pf3/outer", ContractContext().Input, Args, cancellation.Token)
            : ((ContractDsl.IKernelPipeline)Pipeline(new CallCapabilityNode("dsl://pf3/outer", Args), registry))
                .ExecuteAsync(ContractContext(), cancellation.Token);
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken); }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.True(pending.IsCanceled);
        Assert.Equal(1, inner.AsyncCalls);
        Assert.Equal(0, inner.SyncCalls);
    }

    [Fact]
    public async Task LoopUntilChecksCancellationAfterReadingTheClock()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var clock = new TestClock(() => { cancellation.Cancel(); return DateTimeOffset.UnixEpoch; });
        var registry = new SyncRegistry((_, input, _) => { calls++; return Result<DslPipelineValue>.Success(input); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Pipeline(
            new LoopUntilNode(TimeSpan.FromSeconds(30), 3, [Call()]), registry, clock).ExecuteAsync(Context(), cancellation.Token));
        Assert.Equal(0, calls);
    }

    private static DslRomCapabilityRegistry NestedRomRegistry(IDslCapabilityRegistry inner)
    {
        var roms = new DslRomRegistry();
        var registry = new DslRomCapabilityRegistry(inner, roms);
        var provider = new DslRomProvider(new DslPipelineCompiler(registry));
        foreach (var (name, json) in new[]
        {
            ("inner", """{"type":"Pipeline","steps":[{"type":"Loop","maxIterations":2,"body":[{"type":"CallCapability","name":"work"}]}]}"""),
            ("outer", """{"type":"Pipeline","steps":[{"type":"Loop","maxIterations":2,"body":[{"type":"CallCapability","name":"dsl://pf3/inner"}]}]}""")
        })
        {
            var snapshot = provider.CreateSnapshot("pf3", name, json, DateTimeOffset.UnixEpoch);
            Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
            var registered = roms.Register(snapshot.Value!);
            Assert.True(registered.IsSuccess, registered.Error?.Message);
        }
        return registry;
    }

    private static void AssertEquivalent(ResultStep<DslPipelineState, DslPipelineValue> expected,
        ResultStep<DslPipelineState, DslPipelineValue> actual)
    {
        Assert.Equal(expected.IsSuccess, actual.IsSuccess);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(JsonSerializer.Serialize(expected.Value), JsonSerializer.Serialize(actual.Value));
        Assert.Equal(JsonSerializer.Serialize(expected.Error), JsonSerializer.Serialize(actual.Error));
        Assert.Equal(expected.StepId, actual.StepId);
        Assert.Equal(expected.ParentStepId, actual.ParentStepId);
        Assert.Equal(expected.ReplayLogHash, actual.ReplayLogHash);
        Assert.Equal(JsonSerializer.Serialize(expected.ReplayLog), JsonSerializer.Serialize(actual.ReplayLog));
    }

    private sealed class TestClock(Func<DateTimeOffset> now) : IKernelClock
    {
        public TimeProvider Physical => TimeProvider.System;
        public KernelTimeProvider Logical { get; } = new ReplayKernelTimeProvider(DateTimeOffset.UnixEpoch, TimeProvider.System);
        public DateTimeOffset Now => now();
        public bool IsReplaying => true;
        public double ReliabilityScore => 1;
        public KernelTimestamp GetLogicalTimestamp() => new() { UtcDateTime = Now, SourceId = "pf3-test" };
    }
}
