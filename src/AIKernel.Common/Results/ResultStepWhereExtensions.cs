namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultStepWhereExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultStepWhereExtensions']" />
public static class ResultStepWhereExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStepWhereExtensions.TValue&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStepWhereExtensions.TValue&gt;']" />
    public static ResultStep<TState, TValue> Where<TState, TValue>(
        this ResultStep<TState, TValue> step,
        Func<TValue, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (step.IsFailure)
            return step;

        try
        {
            return predicate(step.Value!)
                ? step
                : AppendPredicateFailure(step, PredicateFailedError());
        }
        catch (Exception ex)
        {
            return AppendPredicateFailure(
                step,
                ErrorContext.FromException(ex) with
                {
                    FailureKind = FailureKind.FailClosed,
                    OriginStep = OriginStep.KernelFacade,
                    SemanticSlot = SemanticSlot.T
                });
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStepWhereExtensions.TValue&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStepWhereExtensions.TValue&gt;']" />
    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this ResultStep<TState, TValue> step,
        Func<TValue, Task<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (step.IsFailure)
            return step;

        try
        {
            return await predicate(step.Value!).ConfigureAwait(false)
                ? step
                : AppendPredicateFailure(step, PredicateFailedError());
        }
        catch (Exception ex)
        {
            return AppendPredicateFailure(
                step,
                ErrorContext.FromException(ex) with
                {
                    FailureKind = FailureKind.FailClosed,
                    OriginStep = OriginStep.KernelFacade,
                    SemanticSlot = SemanticSlot.T
                });
        }
    }

    private static ResultStep<TState, TValue> AppendPredicateFailure<TState, TValue>(
        ResultStep<TState, TValue> step,
        ErrorContext error)
        => ResultStep<TState, TValue>
            .Fail(step.State, error)
            .WithReplayLogPrefix(step.ReplayLog)
            .WithSemanticDelta(CreatePredicateDelta(error), LastStepId(step));

    private static SemanticDelta CreatePredicateDelta(ErrorContext error)
        => new(
            "result_step.where",
            OriginStep.KernelFacade,
            SemanticSlot.T,
            Kind: error.FailureKind == FailureKind.Reject ? "reject" : "fail_closed");

    private static ErrorContext PredicateFailedError()
        => new("Predicate failed", "PREDICATE_FAILED", false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };

    private static string? LastStepId<TState, TValue>(
        ResultStep<TState, TValue> step)
        => step.ReplayLog.Count == 0
            ? null
            : step.ReplayLog[^1].StepId;
}
