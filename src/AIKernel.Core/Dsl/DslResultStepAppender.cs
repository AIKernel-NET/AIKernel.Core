namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal static class DslResultStepAppender
{
    /// <summary>
    /// EN: Gets AppendLoopTransition.
    /// EN: Documentation for public API. JA: AppendLoopTransition を取得します。
    /// </summary>
    public static ResultStep<DslPipelineState, DslPipelineValue> AppendLoopTransition(
        ResultStep<DslPipelineState, DslPipelineValue> current,
        int iteration,
        string decision,
        DateTimeOffset? timestamp)
    {
        var delta = OptionalTimestamp(timestamp)
            .Map(value => DslSemanticDeltaFactory.CreateLoopUntilDelta(
                    iteration,
                    value,
                    decision))
            .OrElse(DslSemanticDeltaFactory.CreateLoopDelta(iteration, decision));

        return current.Match(
            (state, error) => AppendFailure(current, state, error, delta),
            (state, value) => AppendSuccess(current, state, value, delta));
    }
    /// <summary>
    /// EN: Gets AppendSuccess.
    /// EN: Documentation for public API. JA: AppendSuccess を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets AppendFailure.
    /// EN: Documentation for public API. JA: AppendFailure を取得します。
    /// </summary>

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
        => LastReplayEntry(current).ToNullableStepId();

    private static Option<ResultStepReplayLogEntry> LastReplayEntry(
        ResultStep<DslPipelineState, DslPipelineValue> current)
    {
        if (current.ReplayLog.Count > 0)
        {
            return Option<ResultStepReplayLogEntry>.Some(current.ReplayLog[^1]);
        }

        return Option<ResultStepReplayLogEntry>.None();
    }

    private static Option<DateTimeOffset> OptionalTimestamp(
        DateTimeOffset? timestamp)
        => timestamp is { } value
            ? Option<DateTimeOffset>.Some(value)
            : Option<DateTimeOffset>.None();

    private static string? ToNullableStepId(
        this Option<ResultStepReplayLogEntry> entry)
        => entry.Match<string?>(
            () => null,
            value => value.StepId);
}
