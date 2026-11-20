namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal sealed partial class CompiledDslPipeline
{
    // Keep the synchronous interpreter independent. The same appenders and deltas preserve its trace format.
    public async Task<ResultStep<DslPipelineState, DslPipelineValue>> ExecuteAsync(
        DslPipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Even an entirely synchronous/empty loop must release the caller's thread and context.
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        cancellationToken.ThrowIfCancellationRequested();
        if (context is null) return InvalidExecutionContext("DSL execution context is required.");
        if (context.Input is null) return InvalidExecutionContext("DSL execution input is required.");

        var initial = ValidatePipelineValue(context.Input, OriginStep.KernelFacade, capabilityName: null)
            .Match(InvalidExecutionContext, _ => ResultStep<DslPipelineState, DslPipelineValue>.Success(
                DslPipelineState.Initial("dsl.pipeline"), context.Input));
        var result = await ExecuteNodeAsync(_root, initial, context with { RemainingSteps = DslPipelineExecutionContext.MaximumSteps }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<ResultStep<DslPipelineState, DslPipelineValue>> ExecuteNodeAsync(
        PipelineNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (current.IsFailure) return current;
        if (!context.SpendStep()) return InvalidExecutionContext("Pipeline execution budget exhausted.");
        return node switch
        {
            PipelineRootNode pipeline => await ExecuteNodesAsync(pipeline.Steps, current, context, cancellationToken).ConfigureAwait(false),
            CallCapabilityNode call => await ExecuteCapabilityAsync(call, current, cancellationToken).ConfigureAwait(false),
            LoopNode loop => await ExecuteLoopAsync(loop, current, context, cancellationToken).ConfigureAwait(false),
            LoopUntilNode loop => await ExecuteLoopUntilAsync(loop, current, context, cancellationToken).ConfigureAwait(false),
            _ => ExecuteNode(node, current, context, countStep: false)
        };
    }

    private async Task<ResultStep<DslPipelineState, DslPipelineValue>> ExecuteNodesAsync(
        IReadOnlyList<PipelineNode> nodes,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (current.IsFailure) break;
            current = await ExecuteNodeAsync(node, current, context, cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return current;
    }

    private async Task<ResultStep<DslPipelineState, DslPipelineValue>> ExecuteCapabilityAsync(
        CallCapabilityNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        CancellationToken cancellationToken)
    {
        Result<DslPipelineValue> invoked;
        try
        {
            invoked = await _capabilityRegistry.InvokeAsync(node.Name, current.Value!, node.Args, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception cancellation) when (CancellationExceptionClassifier.IsCancellation(cancellation)) { throw; }
        catch (Exception exception)
        {
            invoked = Result<DslPipelineValue>.Fail(DslExecutionErrors.CapabilityException(node.Name, exception));
        }
        // A legacy or non-cooperative capability may finish after cancellation; do not publish its value.
        cancellationToken.ThrowIfCancellationRequested();
        var validated = from value in invoked
                        from required in RequireCapabilityValue(node.Name, value)
                        from output in ValidateCapabilityValue(node.Name, required)
                        select output;
        var state = current.State.Advance(node.Name);
        return validated.Match(
            error => DslResultStepAppender.AppendFailure(current, state, error, CreateCapabilityDelta(node, error.Metadata)),
            output => DslResultStepAppender.AppendSuccess(current, state, output, CreateCapabilityDelta(node, output.Data)));
    }

    private async Task<ResultStep<DslPipelineState, DslPipelineValue>> ExecuteLoopAsync(
        LoopNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (node.MaxIterations < 0)
            return DslResultStepAppender.AppendFailure(current, current.State,
                DslExecutionErrors.InvalidRuntime("maxIterations must be greater than or equal to zero."),
                DslSemanticDeltaFactory.CreateLoopDelta(0, "invalid"));

        var result = current;
        for (var iteration = 0; iteration < node.MaxIterations; iteration++)
        {
            if (!context.SpendStep()) return InvalidExecutionContext("Pipeline execution budget exhausted.");
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            cancellationToken.ThrowIfCancellationRequested();
            result = await ExecuteNodesAsync(node.BodyNodes, result, context, cancellationToken).ConfigureAwait(false);
            result = DslResultStepAppender.AppendLoopTransition(result, iteration,
                SelectLoopDecision(iteration, node.MaxIterations), timestamp: null);
            if (result.IsFailure) return result;
        }
        return node.MaxIterations == 0
            ? DslResultStepAppender.AppendLoopTransition(result, 0, "max_iterations_reached", timestamp: null)
            : result;
    }

    private async Task<ResultStep<DslPipelineState, DslPipelineValue>> ExecuteLoopUntilAsync(
        LoopUntilNode node,
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (node.MaxIterations < 0 || node.Timeout < TimeSpan.Zero)
            return DslResultStepAppender.AppendFailure(current, current.State,
                DslExecutionErrors.InvalidRuntime("LoopUntil has invalid bounds."),
                DslSemanticDeltaFactory.CreateLoopUntilDelta(0, null, "invalid"));

        var result = current;
        for (var iteration = 0; iteration < node.MaxIterations; iteration++)
        {
            if (!context.SpendStep()) return InvalidExecutionContext("Pipeline execution budget exhausted.");
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now;
            try { now = _clock.Now; }
            catch (Exception cancellation) when (CancellationExceptionClassifier.IsCancellation(cancellation)) { throw; }
            catch (Exception exception)
            {
                var delta = DslSemanticDeltaFactory.CreateLoopUntilDelta(iteration, timestamp: null, "clock_failed");
                return DslResultStepAppender.AppendFailure(result, result.State,
                    DslExecutionErrors.ClockException(exception, delta), delta);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (now - context.StartedAtUtc >= node.Timeout)
                return DslResultStepAppender.AppendLoopTransition(result, iteration, "timeout_reached", now);

            result = await ExecuteNodesAsync(node.BodyNodes, result, context, cancellationToken).ConfigureAwait(false);
            result = DslResultStepAppender.AppendLoopTransition(result, iteration,
                SelectLoopDecision(iteration, node.MaxIterations), now);
            if (result.IsFailure) return result;
        }
        return DslResultStepAppender.AppendLoopTransition(result, node.MaxIterations,
            "max_iterations_reached", timestamp: null);
    }
}
