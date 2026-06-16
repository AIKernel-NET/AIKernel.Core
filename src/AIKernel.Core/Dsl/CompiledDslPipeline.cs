namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;

internal sealed class CompiledDslPipeline :
    IKernelPipeline,
    AIKernel.Abstractions.Dsl.IKernelPipeline
{
    private readonly PipelineNode _root;
    private readonly IDslCapabilityRegistry _capabilityRegistry;
    private readonly IKernelClock _clock;
    /// <summary>
    /// EN: Gets CompiledDslPipeline.
    /// EN: Documentation for public API. JA: CompiledDslPipeline を取得します。
    /// </summary>

    public CompiledDslPipeline(
        PipelineNode root,
        IDslCapabilityRegistry capabilityRegistry,
        IKernelClock clock)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    /// <summary>
    /// EN: Gets Execute.
    /// EN: Documentation for public API. JA: Execute を取得します。
    /// </summary>

    public ResultStep<DslPipelineState, DslPipelineValue> Execute(
        DslPipelineExecutionContext context)
    {
        if (context is null)
        {
            return InvalidExecutionContext("DSL execution context is required.");
        }

        if (context.Input is null)
        {
            return InvalidExecutionContext("DSL execution input is required.");
        }

        return ValidatePipelineValue(
            context.Input,
            OriginStep.KernelFacade,
            capabilityName: null)
            .Match(
                InvalidExecutionContext,
                _ =>
                {
                    var initial = ResultStep<DslPipelineState, DslPipelineValue>.Success(
                        DslPipelineState.Initial("dsl.pipeline"),
                        context.Input);

                    return ExecuteNode(_root, initial, context);
                });
    }

    Task<AIKernel.Dtos.Dsl.DslPipelineExecutionResult>
        AIKernel.Abstractions.Dsl.IKernelPipeline.ExecuteAsync(
            AIKernel.Dtos.Dsl.DslPipelineExecutionContext context,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = Execute(DslContractMapper.ToCore(context));
        var projection = result.Match(
            (state, error) => (
                Status: ExecutionStatus.Failed,
                State: state,
                Output: DslPipelineValue.Empty,
                Error: (ExecutionError?)new ExecutionError(error.Code, error.Message),
                Metadata: error.Metadata?.ToImmutableDictionary()
                    ?? ImmutableDictionary<string, string>.Empty),
            (state, output) => (
                Status: ExecutionStatus.Succeeded,
                State: state,
                Output: output,
                Error: (ExecutionError?)null,
                Metadata: ImmutableDictionary<string, string>.Empty));

        return Task.FromResult(new AIKernel.Dtos.Dsl.DslPipelineExecutionResult
        {
            Status = projection.Status,
            State = DslContractMapper.ToContract(projection.State),
            Output = DslContractMapper.ToContract(projection.Output),
            Error = projection.Error,
            ReplayLogCount = result.ReplayLog.Count,
            ReplayLogHash = result.ReplayLogHash,
            Metadata = projection.Metadata
        });
    }

    private static ResultStep<DslPipelineState, DslPipelineValue> InvalidExecutionContext(
        string message)
        => InvalidExecutionContext(DslExecutionErrors.InvalidRuntime(message));

    private static ResultStep<DslPipelineState, DslPipelineValue> InvalidExecutionContext(
        ErrorContext error)
    {
        return ResultStep<DslPipelineState, DslPipelineValue>
            .Fail(
                DslPipelineState.Initial("dsl.pipeline"),
                error)
            .WithSemanticDelta(DslSemanticDeltaFactory.CreateNodeDelta(
                "dsl.context.invalid",
                "fail_closed",
                "context",
                "execution_context"));
    }

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteNode(
        PipelineNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context)
    {
        return current.Match(
            (_, _) => current,
            (_, _) => node switch
            {
                PipelineRootNode pipeline => ExecuteNodes(pipeline.Steps, current, context),
                StepNode step => ExecuteStep(step, current),
                CallCapabilityNode call => ExecuteCapability(call, current),
                LoopNode loop => ExecuteLoop(loop, current, context),
                LoopUntilNode loopUntil => ExecuteLoopUntil(loopUntil, current, context),
                SuspendNode suspend => ExecuteSuspend(suspend, current),
                _ => DslResultStepAppender.AppendFailure(
                    current,
                    current.State,
                    DslExecutionErrors.InvalidRuntime(
                        $"Unsupported pipeline node: {node.GetType().Name}."),
                    DslSemanticDeltaFactory.CreateNodeDelta(
                        "dsl.invalid-node",
                        "execute",
                        "invalid",
                        node.Type))
            });
    }

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteNodes(
        IReadOnlyList<PipelineNode> nodes,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context)
    {
        return nodes.Aggregate(
            current,
            (result, node) => result.Match(
                (_, _) => result,
                (_, _) => ExecuteNode(node, result, context)));
    }

    private static ResultStep<DslPipelineState, DslPipelineValue> ExecuteStep(
        StepNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current)
        => current.Match(
            (_, _) => current,
            (state, value) => DslResultStepAppender.AppendSuccess(
                current,
                state.Advance(node.Name),
                value,
                DslSemanticDeltaFactory.CreateNodeDelta(
                    "dsl.step",
                    "execute",
                    "step",
                    node.Name)));

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteCapability(
        CallCapabilityNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current)
        => current.Match(
            (_, _) => current,
            (state, input) =>
            {
                var nextState = state.Advance(node.Name);
                var capabilityResult =
                    from invoked in InvokeCapability(node, input)
                    from value in RequireCapabilityValue(node.Name, invoked)
                    from validated in ValidateCapabilityValue(node.Name, value)
                    select validated;

                return capabilityResult.Match(
                    error => DslResultStepAppender.AppendFailure(
                            current,
                            nextState,
                            error,
                            CreateCapabilityDelta(node, error.Metadata)),
                    output => DslResultStepAppender.AppendSuccess(
                        current,
                        nextState,
                        output,
                        CreateCapabilityDelta(node, output.Data)));
            });

    private Result<DslPipelineValue> InvokeCapability(
        CallCapabilityNode node,
        DslPipelineValue input)
        => Try
            .Run(() => _capabilityRegistry.Invoke(
                node.Name,
                input,
                node.Args))
            .Match(
                error => Result<DslPipelineValue>.Fail(
                    DslExecutionErrors.CapabilityException(node.Name, error)),
                result => result);

    private static Result<DslPipelineValue> RequireCapabilityValue(
        string capabilityName,
        DslPipelineValue? value)
    {
        return value is null
            ? Result<DslPipelineValue>.Fail(
                DslExecutionErrors.CapabilityReturnedNull(capabilityName))
            : Result<DslPipelineValue>.Success(value);
    }

    private static Result<DslPipelineValue> ValidateCapabilityValue(
        string capabilityName,
        DslPipelineValue value)
        => ValidatePipelineValue(value, OriginStep.Capability, capabilityName)
            .Map(_ => value);

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteLoop(
        LoopNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context)
    {
        if (node.MaxIterations < 0)
        {
            return DslResultStepAppender.AppendFailure(
                current,
                current.State,
                DslExecutionErrors.InvalidRuntime(
                    "maxIterations must be greater than or equal to zero."),
                DslSemanticDeltaFactory.CreateLoopDelta(0, "invalid"));
        }

        var result = current;
        for (var iteration = 0; iteration < node.MaxIterations; iteration++)
        {
            result = ExecuteNodes(node.BodyNodes, result, context);
            result = DslResultStepAppender.AppendLoopTransition(
                result,
                iteration,
                SelectLoopDecision(iteration, node.MaxIterations),
                timestamp: null);

            var stopped = StopWhenFailed(result)
                .Match<ResultStep<DslPipelineState, DslPipelineValue>?>(
                    () => null,
                    failed => failed);
            if (stopped is { } failed)
                return failed;
        }

        return node.MaxIterations == 0
            ? DslResultStepAppender.AppendLoopTransition(
                result,
                0,
                "max_iterations_reached",
                timestamp: null)
            : result;
    }

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteLoopUntil(
        LoopUntilNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context)
    {
        if (node.MaxIterations < 0 || node.Timeout < TimeSpan.Zero)
        {
            return DslResultStepAppender.AppendFailure(
                current,
                current.State,
                DslExecutionErrors.InvalidRuntime("LoopUntil has invalid bounds."),
                DslSemanticDeltaFactory.CreateLoopUntilDelta(0, null, "invalid"));
        }

        var result = current;
        for (var iteration = 0; iteration < node.MaxIterations; iteration++)
        {
            var nowOrFailure = Try.Run(() => _clock.Now)
                .Match(
                    error => (Error: error, Now: (DateTimeOffset?)null),
                    now => (Error: (ErrorContext?)null, Now: (DateTimeOffset?)now));
            if (nowOrFailure.Error is not null)
            {
                var delta = DslSemanticDeltaFactory.CreateLoopUntilDelta(
                    iteration,
                    timestamp: null,
                    "clock_failed");

                return DslResultStepAppender.AppendFailure(
                    result,
                    result.State,
                    DslExecutionErrors.ClockException(nowOrFailure.Error, delta),
                    delta);
            }

            var now = nowOrFailure.Now.GetValueOrDefault();

            if (now - context.StartedAtUtc >= node.Timeout)
            {
                return DslResultStepAppender.AppendLoopTransition(
                    result,
                    iteration,
                    "timeout_reached",
                    now);
            }

            result = ExecuteNodes(node.BodyNodes, result, context);
            result = DslResultStepAppender.AppendLoopTransition(
                result,
                iteration,
                SelectLoopDecision(iteration, node.MaxIterations),
                now);

            var stopped = StopWhenFailed(result)
                .Match<ResultStep<DslPipelineState, DslPipelineValue>?>(
                    () => null,
                    failed => failed);
            if (stopped is { } failed)
                return failed;
        }

        return DslResultStepAppender.AppendLoopTransition(
            result,
            node.MaxIterations,
            "max_iterations_reached",
            timestamp: null);
    }

    private static ResultStep<DslPipelineState, DslPipelineValue> ExecuteSuspend(
        SuspendNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current)
    {
        return PipelineStep
            .Suspend<DslPipelineState, DslPipelineValue>(
                current.State.Advance("suspend"),
                node.Reason)
            .WithReplayLogPrefix(current.ReplayLog);
    }

    private static Result<bool> ValidatePipelineValue(
        DslPipelineValue value,
        OriginStep originStep,
        string? capabilityName)
        => from data in RequirePipelineData(value)
                .ToPipelineValueResult(originStep, capabilityName)
           from _ in ValidatePipelineData(data)
                .ToPipelineValueResult(originStep, capabilityName)
           select true;

    private static Either<string, IReadOnlyDictionary<string, string>> RequirePipelineData(
        DslPipelineValue value)
        => value.Data is null
            ? Either<string, IReadOnlyDictionary<string, string>>.FromLeft(
                "DSL pipeline value data is required.")
            : Either<string, IReadOnlyDictionary<string, string>>.FromRight(value.Data);

    private static Either<string, bool> ValidatePipelineData(
        IReadOnlyDictionary<string, string> data)
        => data.Aggregate(
            Either<string, bool>.FromRight(true),
            (current, item) =>
                from _ in current
                from __ in ValidatePipelineEntry(item)
                select true);

    private static Either<string, bool> ValidatePipelineEntry(
        KeyValuePair<string, string> item)
    {
        if (item.Key is null)
        {
            return Either<string, bool>.FromLeft(
                "DSL pipeline value data keys must not be null.");
        }

        if (string.IsNullOrWhiteSpace(item.Key))
        {
            return Either<string, bool>.FromLeft(
                "DSL pipeline value data keys must not be empty.");
        }

        if (item.Value is null)
        {
            return Either<string, bool>.FromLeft(
                "DSL pipeline value data values must not be null.");
        }

        return Either<string, bool>.FromRight(true);
    }

    private static SemanticDelta CreateCapabilityDelta(
        CallCapabilityNode node,
        IReadOnlyDictionary<string, string>? sourceMetadata = null)
    {
        var romMetadata = CreateRomMetadata(sourceMetadata);

        return DslSemanticDeltaFactory.CreateNodeDelta(
            "dsl.capability.call",
            "execute",
            "call_capability",
            node.Name,
            node.Args,
            romMetadata);
    }

    private static Dictionary<string, string>? CreateRomMetadata(
        IReadOnlyDictionary<string, string>? source)
        => ReadRomMetadata(source)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal) is { Count: > 0 } metadata
                ? metadata
                : null;

    private static IEnumerable<KeyValuePair<string, string>> ReadRomMetadata(
        IReadOnlyDictionary<string, string>? source)
        => RomMetadataKeys()
            .Select(key => ReadMetadata(source, key)
                .Map(value => new KeyValuePair<string, string>(key, value)))
            .SelectMany(option => option.Match(
                Enumerable.Empty<KeyValuePair<string, string>>,
                value => [value]));

    private static string[] RomMetadataKeys()
        =>
        [
            DslRomMetadataKeys.RomCall,
            DslRomMetadataKeys.RomHash,
            DslRomMetadataKeys.RomPath,
            DslRomMetadataKeys.RomNamespace,
            DslRomMetadataKeys.RomName,
            DslRomMetadataKeys.RomReplayLogCount,
            DslRomMetadataKeys.RomReplayLogHash
        ];

    private static string SelectLoopDecision(
        int iteration,
        int maxIterations)
        => IsFinalLoopIteration(iteration, maxIterations)
            .Match(
                _ => "continue",
                _ => "max_iterations_reached");

    private static Either<string, int> IsFinalLoopIteration(
        int iteration,
        int maxIterations)
    {
        if (iteration == maxIterations - 1)
        {
            return Either<string, int>.FromRight(iteration);
        }

        return Either<string, int>.FromLeft("Loop has remaining iterations.");
    }

    private static Option<ResultStep<DslPipelineState, DslPipelineValue>> StopWhenFailed(
        ResultStep<DslPipelineState, DslPipelineValue> result)
        => result.Match(
            (_, _) => Option<ResultStep<DslPipelineState, DslPipelineValue>>.Some(result),
            (_, _) => Option<ResultStep<DslPipelineState, DslPipelineValue>>.None());

    private static Option<string> ReadMetadata(
        IReadOnlyDictionary<string, string>? source,
        string key)
    {
        if (source is not null &&
            source.TryGetValue(key, out var value))
        {
            return Option<string>.Some(value);
        }

        return Option<string>.None();
    }

}

internal static class CompiledDslPipelineEitherExtensions
{
    /// <summary>
    /// EN: Gets ToPipelineValueResult&lt;T&gt;.
    /// EN: Documentation for public API. JA: ToPipelineValueResult&lt;T&gt; を取得します。
    /// </summary>
    public static Result<T> ToPipelineValueResult<T>(
        this Either<string, T> value,
        OriginStep originStep,
        string? capabilityName)
        => value.Match(
            left => Result<T>.Fail(DslExecutionErrors.InvalidPipelineValue(
                left,
                originStep,
                capabilityName)),
            Result<T>.Success);
}
