namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal static class DslResultStepAppender
{
    public static ResultStep<DslPipelineState, DslPipelineValue> AppendLoopTransition(
        ResultStep<DslPipelineState, DslPipelineValue> current,
        int iteration,
        string decision,
        DateTimeOffset? timestamp)
    {
        var delta = timestamp is null
            ? DslSemanticDeltaFactory.CreateLoopDelta(iteration, decision)
            : DslSemanticDeltaFactory.CreateLoopUntilDelta(iteration, timestamp, decision);

        return current.IsSuccess
            ? AppendSuccess(current, current.State, current.Value!, delta)
            : AppendFailure(current, current.State, current.Error!, delta);
    }

    public static ResultStep<DslPipelineState, DslPipelineValue> AppendSuccess(
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineState state,
        DslPipelineValue value,
        SemanticDelta delta)
    {
        return ResultStep<DslPipelineState, DslPipelineValue>
            .Success(state, value)
            .WithReplayLogPrefix(current.ReplayLog)
            .WithSemanticDelta(delta, LastStepId(current));
    }

    public static ResultStep<DslPipelineState, DslPipelineValue> AppendFailure(
        ResultStep<DslPipelineState, DslPipelineValue> current,
        DslPipelineState state,
        ErrorContext error,
        SemanticDelta delta)
    {
        return ResultStep<DslPipelineState, DslPipelineValue>
            .Fail(state, error)
            .WithReplayLogPrefix(current.ReplayLog)
            .WithSemanticDelta(delta, LastStepId(current));
    }

    private static string? LastStepId(
        ResultStep<DslPipelineState, DslPipelineValue> current)
        => current.ReplayLog.Count == 0
            ? null
            : current.ReplayLog[^1].StepId;
}
