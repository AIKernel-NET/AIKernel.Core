namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Time;

internal sealed class CompiledDslPipeline : IKernelPipeline
{
    private readonly PipelineNode _root;
    private readonly IDslCapabilityRegistry _capabilityRegistry;
    private readonly IKernelClock _clock;

    public CompiledDslPipeline(
        PipelineNode root,
        IDslCapabilityRegistry capabilityRegistry,
        IKernelClock clock)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ResultStep<DslPipelineState, DslPipelineValue> Execute(
        DslPipelineExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var initial = ResultStep<DslPipelineState, DslPipelineValue>.Success(
            DslPipelineState.Initial("dsl.pipeline"),
            context.Input);

        return ExecuteNode(_root, initial, context);
    }

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteNode(
        PipelineNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context)
    {
        if (current.IsFailure)
            return current;

        return node switch
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
        };
    }

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteNodes(
        IReadOnlyList<PipelineNode> nodes,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context)
    {
        var result = current;
        foreach (var node in nodes)
        {
            result = ExecuteNode(node, result, context);
            if (result.IsFailure)
                return result;
        }

        return result;
    }

    private static ResultStep<DslPipelineState, DslPipelineValue> ExecuteStep(
        StepNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current)
    {
        return DslResultStepAppender.AppendSuccess(
            current,
            current.State.Advance(node.Name),
            current.Value!,
            DslSemanticDeltaFactory.CreateNodeDelta(
                "dsl.step",
                "execute",
                "step",
                node.Name));
    }

    private ResultStep<DslPipelineState, DslPipelineValue> ExecuteCapability(
        CallCapabilityNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current)
    {
        var nextState = current.State.Advance(node.Name);
        var capabilityResult = _capabilityRegistry.Invoke(
            node.Name,
            current.Value!,
            node.Args);
        var delta = DslSemanticDeltaFactory.CreateNodeDelta(
            "dsl.capability.call",
            "execute",
            "call_capability",
            node.Name,
            node.Args);

        return capabilityResult.IsSuccess
            ? DslResultStepAppender.AppendSuccess(current, nextState, capabilityResult.Value!, delta)
            : DslResultStepAppender.AppendFailure(current, nextState, capabilityResult.Error!, delta);
    }

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
                iteration == node.MaxIterations - 1
                    ? "max_iterations_reached"
                    : "continue",
                timestamp: null);

            if (result.IsFailure)
                return result;
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
            var now = _clock.Now;
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
                iteration == node.MaxIterations - 1
                    ? "max_iterations_reached"
                    : "continue",
                now);

            if (result.IsFailure)
                return result;
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

}
