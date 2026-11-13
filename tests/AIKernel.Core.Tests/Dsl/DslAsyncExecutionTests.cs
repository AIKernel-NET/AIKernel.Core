namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Time;
using AIKernel.Core.Providers.LocalExecutionProvider;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Time;
using ContractDsl = AIKernel.Abstractions.Dsl;
using DslDtos = AIKernel.Dtos.Dsl;

public sealed partial class DslAsyncExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicPipelineCancelsAfterFirstLoopBody(bool until)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var registry = new SyncRegistry((_, input, _) =>
        {
            calls++;
            cancellation.Cancel();
            return Result<DslPipelineValue>.Success(input);
        });
        PipelineNode[] body = [new CallCapabilityNode("work", new Dictionary<string, string>())];
        PipelineNode loop = until ? new LoopUntilNode(TimeSpan.FromSeconds(30), 3, body) : new LoopNode(3, body);
        var compiled = new DslPipelineCompiler(registry).Compile(new DslDocument(new PipelineRootNode([loop])));
        Assert.True(compiled.IsSuccess);
        var pipeline = Assert.IsAssignableFrom<ContractDsl.IKernelPipeline>(compiled.Value);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(
            new DslDtos.DslPipelineExecutionContext(new DslDtos.DslPipelineValue(new Dictionary<string, string>()),
                DateTimeOffset.UnixEpoch), cancellation.Token));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task PreCanceledPublicEntryPointsDoNotInvokeCapabilities()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var registry = new AsyncRegistry((_, _, _) => throw new InvalidOperationException("Must not start."));
        ContractDsl.IKernelPipeline pipeline = Pipeline(Call(), registry);
        ContractDsl.IDslCapabilityRegistry rom = new DslRomCapabilityRegistry(registry, new DslRomRegistry());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(ContractContext(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rom.InvokeAsync("work", ContractContext().Input, Args, cancellation.Token));
        Assert.Equal(0, registry.AsyncCalls);
        Assert.Equal(0, registry.SyncCalls);
    }

    [Fact]
    public async Task AsyncCapabilityIsAwaitedAndReceivesTheExecutionToken()
    {
        using var cancellation = new CancellationTokenSource();
        var started = Signal();
        var release = Signal();
        var registry = new AsyncRegistry(async (input, _, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            started.SetResult();
            await release.Task.WaitAsync(token);
            return Result<DslPipelineValue>.Success(input.With("answer", "42"));
        });
        var pending = Pipeline(Call(), registry).ExecuteAsync(Context(), cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.False(pending.IsCompleted);
        }
        finally { release.TrySetResult(); }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value!.Data["answer"]);
        Assert.Equal(1, registry.AsyncCalls);
        Assert.Equal(0, registry.SyncCalls);
    }

    [Fact]
    public async Task CancellationReachesAnAwaitingCapabilityAndStopsTheNestedLoop()
    {
        using var cancellation = new CancellationTokenSource();
        var started = Signal();
        var registry = new AsyncRegistry(async (input, _, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result<DslPipelineValue>.Success(input);
        });
        var pending = Pipeline(new LoopNode(3, [new LoopNode(3, [Call(), Call()])]), registry)
            .ExecuteAsync(Context(), cancellation.Token);
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken); }
        finally { cancellation.Cancel(); }
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(pending.IsCanceled);
        Assert.Equal(1, registry.AsyncCalls);
        Assert.Equal(0, registry.SyncCalls);
    }

    [Fact]
    public async Task NonCooperativeCapabilityIsNotDetachedAndItsLateValueIsDiscarded()
    {
        using var cancellation = new CancellationTokenSource();
        var started = Signal();
        var release = Signal();
        var registry = new AsyncRegistry(async (input, _, _) =>
        {
            started.SetResult();
            await release.Task;
            return Result<DslPipelineValue>.Success(input.With("late", "must-not-escape"));
        });
        var pending = Pipeline(new LoopNode(3, [Call()]), registry).ExecuteAsync(Context(), cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Equal(1, registry.AsyncCalls);
    }

    [Fact]
    public async Task SynchronousCapabilityDoesNotBlockTheCallingThreadOrCaptureItsContext()
    {
        using var cancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var returned = new TaskCompletionSource<Task<DslDtos.DslPipelineExecutionResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new SyncRegistry((_, input, _) =>
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Test release missing.");
            return Result<DslPipelineValue>.Success(input);
        });
        var caller = Task.Run(() =>
        {
            var old = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new RejectingContext());
                ContractDsl.IKernelPipeline pipeline = Pipeline(Call(), registry);
                returned.SetResult(pipeline.ExecuteAsync(ContractContext(), cancellation.Token));
            }
            finally { SynchronizationContext.SetSynchronizationContext(old); }
        }, TestContext.Current.CancellationToken);
        Task<DslDtos.DslPipelineExecutionResult>? pending = null;
        try
        {
            pending = await returned.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
        }
        finally { release.Set(); }
        await caller.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending!.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyLoopBodiesStillObserveCancellationBetweenIterations(bool until)
    {
        using var cancellation = new CancellationTokenSource();
        var body = new CancelingEmptyBody(cancellation);
        PipelineNode loop = until ? new LoopUntilNode(TimeSpan.FromSeconds(30), int.MaxValue, body)
                                 : new LoopNode(int.MaxValue, body);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Pipeline(loop).ExecuteAsync(Context(), cancellation.Token));
        Assert.Equal(2, body.Enumerations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationExceptionSubtypesFromLinkedTokensAreNotConvertedToDslFailures(bool taskCanceled)
    {
        using var parent = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(parent.Token);
        linked.Cancel();
        var registry = new AsyncRegistry((_, _, _) => Task.FromException<Result<DslPipelineValue>>(
            taskCanceled ? new TaskCanceledException("Canceled", null, linked.Token) : new DerivedCancellation(linked.Token)));
        var task = Pipeline(Call(), registry).ExecuteAsync(Context(), parent.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(linked.Token, error.CancellationToken);
        Assert.True(task.IsCanceled);
        Assert.False(parent.IsCancellationRequested);
    }

    [Fact]
    public async Task LocalExecutionPropagatesMidLoopCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var registry = new SyncRegistry((_, input, _) =>
        {
            calls++;
            cancellation.Cancel();
            return Result<DslPipelineValue>.Success(input);
        });
        var invoker = new LocalExecutionInvoker(new DslPipelineCompiler(registry));
        var request = Request("""{"type":"Pipeline","steps":[{"type":"Loop","maxIterations":3,"body":[{"type":"CallCapability","name":"work"}]}]}""");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.InvokeAsync(request, cancellation.Token).AsTask());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LocalExecutionDoesNotSwallowCompilerCancellation()
    {
        var invoker = new LocalExecutionInvoker(new CancelingCompiler());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.InvokeAsync(
            Request("""{"type":"Pipeline","steps":[]}"""), TestContext.Current.CancellationToken).AsTask());
    }

    private static readonly IReadOnlyDictionary<string, string> Args = new Dictionary<string, string>();
    private static CallCapabilityNode Call() => new("work", Args);
    private static DslPipelineExecutionContext Context() => DslPipelineExecutionContext.Create();
    private static DslDtos.DslPipelineExecutionContext ContractContext() => new(new DslDtos.DslPipelineValue(Args), DateTimeOffset.UnixEpoch);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static CompiledDslPipeline Pipeline(PipelineNode node, IDslCapabilityRegistry? registry = null, IKernelClock? clock = null)
        => new(new PipelineRootNode([node]), registry ?? new SyncRegistry((_, input, _) => Result<DslPipelineValue>.Success(input)),
            clock ?? KernelClock.Replay(DateTimeOffset.UnixEpoch));
    private static CapabilityInvocationRequest Request(string json) => new("pf3", "aikernel.local.execute", "pipeline.execute",
        new Dictionary<string, string> { ["pipeline.json"] = json }, null, null, new Dictionary<string, string>());

    private sealed class AsyncRegistry(Func<DslPipelineValue, IReadOnlyDictionary<string, string>, CancellationToken, Task<Result<DslPipelineValue>>> invoke)
        : IDslCapabilityRegistry
    {
        public int SyncCalls;
        public int AsyncCalls;
        public bool Contains(string name) => true;
        public Result<DslPipelineValue> Invoke(string name, DslPipelineValue input, IReadOnlyDictionary<string, string> args)
        {
            Interlocked.Increment(ref SyncCalls);
            throw new InvalidOperationException("Synchronous path used for asynchronous capability.");
        }
        public Task<Result<DslPipelineValue>> InvokeAsync(string name, DslPipelineValue input,
            IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref AsyncCalls);
            return invoke(input, args, cancellationToken);
        }
    }
    private sealed class CancelingEmptyBody(CancellationTokenSource cancellation) : IReadOnlyList<PipelineNode>
    {
        public int Enumerations;
        public int Count => 0;
        public PipelineNode this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<PipelineNode> GetEnumerator()
        {
            if (++Enumerations == 2) cancellation.Cancel();
            return Enumerable.Empty<PipelineNode>().GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class RejectingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => throw new InvalidOperationException("Caller context captured.");
    }
    private sealed class DerivedCancellation(CancellationToken token) : OperationCanceledException(token);
    private sealed class CancelingCompiler : ContractDsl.IDslPipelineCompiler
    {
        public Task<ContractDsl.IKernelPipeline> CompileAsync(DslDtos.DslDocument document, CancellationToken cancellationToken = default)
            => Task.FromException<ContractDsl.IKernelPipeline>(new TaskCanceledException());
    }

    private sealed class SyncRegistry(
        Func<string, DslPipelineValue, IReadOnlyDictionary<string, string>, Result<DslPipelineValue>> invoke)
        : IDslCapabilityRegistry
    {
        public bool Contains(string name) => true;
        public Result<DslPipelineValue> Invoke(string name, DslPipelineValue input, IReadOnlyDictionary<string, string> args)
            => invoke(name, input, args);
    }
}
