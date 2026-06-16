namespace AIKernel.Common.Results;

using System.Collections.Immutable;
using System.Globalization;

/// <summary>EN: Documentation for public API. JA: PipelineStep を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.PipelineStep']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.PipelineStep']/summary" />
public static class PipelineStep
{
    /// <summary>EN: Documentation for public API. JA: SuspendErrorCode 定数を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.PipelineStep.SuspendErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.PipelineStep.SuspendErrorCode']/summary" />
    public const string SuspendErrorCode = "SUSPENDED";

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    public static ResultStep<TState, TValue> Loop<TState, TValue>(
        ResultStep<TState, TValue> initial,
        int maxIterations,
        Func<int, TValue, ResultStep<TState, TValue>> stepFunc)
    {
        ArgumentNullException.ThrowIfNull(stepFunc);

        if (maxIterations < 0)
        {
            return InvalidLoop<TState, TValue>(
                initial.State,
                "maxIterations must be greater than or equal to zero.");
        }

        var current = initial;
        if (current.IsFailure)
            return current;

        if (maxIterations == 0)
        {
            return current.WithSemanticDelta(
                CreateLoopDelta(
                    iteration: 0,
                    decision: "max_iterations_reached"));
        }

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var currentIteration = iteration;
            current = ApplyLoopIteration(
                current,
                value => stepFunc(currentIteration, value),
                CreateLoopDelta(
                    currentIteration,
                    currentIteration == maxIterations - 1
                        ? "max_iterations_reached"
                        : "continue"));

            if (current.IsFailure)
                return current;
        }

        return current;
    }

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    public static ResultStep<TState, TValue> LoopUntil<TState, TValue>(
        ResultStep<TState, TValue> initial,
        TimeSpan timeout,
        DateTimeOffset startedAtUtc,
        Func<DateTimeOffset> nowProvider,
        int maxIterations,
        Func<int, DateTimeOffset, TValue, ResultStep<TState, TValue>> stepFunc)
    {
        ArgumentNullException.ThrowIfNull(nowProvider);
        ArgumentNullException.ThrowIfNull(stepFunc);

        if (timeout < TimeSpan.Zero)
        {
            return InvalidLoop<TState, TValue>(
                initial.State,
                "timeout must be greater than or equal to zero.");
        }

        if (maxIterations < 0)
        {
            return InvalidLoop<TState, TValue>(
                initial.State,
                "maxIterations must be greater than or equal to zero.");
        }

        var current = initial;
        if (current.IsFailure)
            return current;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            DateTimeOffset now;
            try
            {
                now = nowProvider();
            }
            catch (Exception ex)
            {
                return current
                    .FailLoopIteration(
                        ex,
                        CreateLoopUntilDelta(
                            iteration,
                            timestamp: null,
                            decision: "clock_failed"));
            }

            if (now - startedAtUtc >= timeout)
            {
                return current.WithSemanticDelta(
                    CreateLoopUntilDelta(
                        iteration,
                        now,
                        "timeout_reached"));
            }

            var currentIteration = iteration;
            current = ApplyLoopIteration(
                current,
                value => stepFunc(currentIteration, now, value),
                CreateLoopUntilDelta(
                    currentIteration,
                    now,
                    currentIteration == maxIterations - 1
                        ? "max_iterations_reached"
                        : "continue"));

            if (current.IsFailure)
                return current;
        }

        return current.WithSemanticDelta(
            CreateLoopUntilDelta(
                maxIterations,
                timestamp: null,
                decision: "max_iterations_reached"));
    }

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    public static ResultStep<TState, TValue> Suspend<TState, TValue>(
        TState state,
        string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Pipeline suspended."
            : reason;
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add(PipelineStepMetadataKeys.DeltaKind, "suspend")
            .Add(PipelineStepMetadataKeys.SuspendReason, normalizedReason);
        var error = new ErrorContext(
            normalizedReason,
            SuspendErrorCode,
            IsRetryable: true)
        {
            FailureKind = FailureKind.Quarantine,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T,
            Metadata = metadata
        };

        return ResultStep<TState, TValue>
            .Fail(state, error)
            .WithSemanticDelta(new SemanticDelta(
                "pipeline.suspend",
                OriginStep.KernelFacade,
                SemanticSlot.T,
                metadata,
                Kind: "suspend"));
    }

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.PipelineStep.TValue&gt;']/summary" />
    public static ResultStep<TState, TValue> Resume<TState, TValue>(
        IReadOnlyList<ResultStepReplayLogEntry> previousReplayLog,
        TState state,
        TValue value,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(previousReplayLog);

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Pipeline resumed."
            : reason;
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add(PipelineStepMetadataKeys.DeltaKind, "resume")
            .Add(PipelineStepMetadataKeys.ResumeReason, normalizedReason)
            .Add(PipelineStepMetadataKeys.PreviousReplayLogCount, previousReplayLog.Count.ToString(CultureInfo.InvariantCulture))
            .Add(PipelineStepMetadataKeys.PreviousReplayLogHash, ResultStepIdentity.CreateReplayLogHash(previousReplayLog));

        return ResultStep<TState, TValue>
            .Success(state, value)
            .WithReplayLogPrefix(previousReplayLog)
            .WithSemanticDelta(new SemanticDelta(
                "pipeline.resume",
                OriginStep.KernelFacade,
                SemanticSlot.T,
                metadata,
                Kind: "resume"),
                previousReplayLog.Count == 0
                    ? null
                    : previousReplayLog[^1].StepId);
    }

    private static ResultStep<TState, TValue> ApplyLoopIteration<TState, TValue>(
        ResultStep<TState, TValue> current,
        Func<TValue, ResultStep<TState, TValue>> stepFunc,
        SemanticDelta loopDelta)
    {
        var replayLogPrefix = current.ReplayLog;

        ResultStep<TState, TValue> next;
        try
        {
            next = stepFunc(current.Value!);
        }
        catch (Exception ex)
        {
            return current.FailLoopIteration(ex, loopDelta);
        }

        var combined = next.WithReplayLogPrefix(replayLogPrefix);
        var parentStepId = ResolveLoopDeltaParentStepId(
            replayLogPrefix,
            combined);

        return combined.WithSemanticDelta(loopDelta, parentStepId);
    }

    private static string? ResolveLoopDeltaParentStepId<TState, TValue>(
        IReadOnlyList<ResultStepReplayLogEntry> replayLogPrefix,
        ResultStep<TState, TValue> combined)
    {
        return combined.ReplayLog.Count > replayLogPrefix.Count
            ? combined.ReplayLog[^1].StepId
            : replayLogPrefix.Count == 0
                ? null
                : replayLogPrefix[^1].StepId;
    }

    private static ResultStep<TState, TValue> InvalidLoop<TState, TValue>(
        TState state,
        string message)
    {
        return ResultStep<TState, TValue>
            .Fail(
                state,
                new ErrorContext(message, "INVALID_PIPELINE_LOOP", false)
                {
                    FailureKind = FailureKind.Reject,
                    OriginStep = OriginStep.KernelFacade,
                    SemanticSlot = SemanticSlot.T
                })
            .WithSemanticDelta(new SemanticDelta(
                "pipeline.loop.invalid",
                OriginStep.KernelFacade,
                SemanticSlot.T,
                ImmutableDictionary<string, string>.Empty
                    .Add(PipelineStepMetadataKeys.DeltaKind, "loop")
                    .Add(PipelineStepMetadataKeys.LoopDecision, "invalid"),
                Kind: "loop"));
    }

    private static ResultStep<TState, TValue> FailLoopIteration<TState, TValue>(
        this ResultStep<TState, TValue> current,
        Exception exception,
        SemanticDelta loopDelta)
    {
        var exceptionError = ErrorContext.FromException(exception);
        var error = exceptionError with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T,
            Metadata = MergeMetadata(
                exceptionError.Metadata,
                loopDelta.Metadata)
        };
        var parentStepId = current.ReplayLog.Count == 0
            ? null
            : current.ReplayLog[^1].StepId;

        return ResultStep<TState, TValue>
            .Fail(current.State, error)
            .WithReplayLogPrefix(current.ReplayLog)
            .WithSemanticDelta(loopDelta, parentStepId);
    }

    private static IReadOnlyDictionary<string, string>? MergeMetadata(
        IReadOnlyDictionary<string, string>? first,
        IReadOnlyDictionary<string, string>? second)
    {
        if (first is null)
            return second;

        if (second is null)
            return first;

        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        foreach (var item in first)
        {
            builder[item.Key] = item.Value;
        }

        foreach (var item in second)
        {
            builder[item.Key] = item.Value;
        }

        return builder.ToImmutable();
    }

    private static SemanticDelta CreateLoopDelta(
        int iteration,
        string decision)
    {
        return new SemanticDelta(
            "pipeline.loop.iteration",
            OriginStep.KernelFacade,
            SemanticSlot.T,
            ImmutableDictionary<string, string>.Empty
                .Add(PipelineStepMetadataKeys.DeltaKind, "loop")
                .Add(PipelineStepMetadataKeys.LoopIteration, iteration.ToString(CultureInfo.InvariantCulture))
                .Add(PipelineStepMetadataKeys.LoopDecision, decision),
            Kind: "loop");
    }

    private static SemanticDelta CreateLoopUntilDelta(
        int iteration,
        DateTimeOffset? timestamp,
        string decision)
    {
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add(PipelineStepMetadataKeys.DeltaKind, "loop")
            .Add(PipelineStepMetadataKeys.LoopIteration, iteration.ToString(CultureInfo.InvariantCulture))
            .Add(PipelineStepMetadataKeys.LoopDecision, decision);

        if (timestamp is { } value)
        {
            metadata = metadata.Add(PipelineStepMetadataKeys.LoopTimestamp, value.ToString("O", CultureInfo.InvariantCulture));
        }

        return new SemanticDelta(
            "pipeline.loop_until.iteration",
            OriginStep.KernelFacade,
            SemanticSlot.T,
            metadata,
            Kind: "loop");
    }
}
