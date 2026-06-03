namespace AIKernel.Common.Results;

public readonly struct ResultStep<TState, TValue>
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public TState State { get; }

    public TValue? Value { get; }

    public ErrorContext? Error { get; }

    public string StepId { get; }

    public SemanticDelta SemanticDelta { get; }

    public string? ParentStepId { get; }

    public IReadOnlyList<ResultStepReplayLogEntry> ReplayLog { get; }

    public string ReplayLogHash { get; }

    private ResultStep(
        bool isSuccess,
        TState state,
        TValue? value,
        ErrorContext? error,
        string? stepId,
        SemanticDelta? semanticDelta,
        string? parentStepId,
        IReadOnlyList<ResultStepReplayLogEntry>? replayLog)
    {
        IsSuccess = isSuccess;
        State = state;
        Value = value;
        Error = error;
        SemanticDelta = semanticDelta ?? SemanticDelta.Empty;
        ParentStepId = parentStepId;
        StepId = string.IsNullOrWhiteSpace(stepId)
            ? ResultStepIdentity.Create(
                parentStepId,
                SemanticDelta,
                IsSuccess,
                Error?.Code)
            : stepId;
        ReplayLog = replayLog ?? Array.Empty<ResultStepReplayLogEntry>();
        ReplayLogHash = ResultStepIdentity.CreateReplayLogHash(ReplayLog);
    }

    public static ResultStep<TState, TValue> Success(
        TState state,
        TValue value)
        => new(
            true,
            state,
            value,
            null,
            stepId: null,
            semanticDelta: null,
            parentStepId: null,
            replayLog: null);

    public static ResultStep<TState, TValue> Fail(
        TState state,
        ErrorContext error)
        => new(
            false,
            state,
            default,
            error,
            stepId: null,
            semanticDelta: null,
            parentStepId: null,
            replayLog: null);

    public static ResultStep<TState, TValue> FromResult(
        TState state,
        Result<TValue> result)
        => result.IsSuccess
            ? Success(state, result.Value!)
            : Fail(state, result.Error!);

    public ResultStep<TState, TValue> WithState(TState state)
        => IsSuccess
            ? new(
                true,
                state,
                Value!,
                null,
                StepId,
                SemanticDelta,
                ParentStepId,
                ReplayLog)
            : new(
                false,
                state,
                default,
                Error!,
                StepId,
                SemanticDelta,
                ParentStepId,
                ReplayLog);

    public ResultStep<TState, TValue> WithSemanticDelta(
        SemanticDelta semanticDelta,
        string? parentStepId = null)
    {
        var resolvedParentStepId = parentStepId ?? StepId;
        var stepId = ResultStepIdentity.Create(
            resolvedParentStepId,
            semanticDelta,
            IsSuccess,
            Error?.Code);
        var replayLog = AppendReplayLogEntry(
            ReplayLog,
            new ResultStepReplayLogEntry(
                stepId,
                resolvedParentStepId,
                semanticDelta,
                IsSuccess,
                Error?.Code));

        return new(
            IsSuccess,
            State,
            Value,
            Error,
            stepId,
            semanticDelta,
            resolvedParentStepId,
            replayLog);
    }

    public ResultStep<TState, TValue> MapState(
        Func<TState, TValue, TState> mapper)
    {
        if (IsFailure)
            return this;

        try
        {
            return new(
                true,
                mapper(State, Value!),
                Value!,
                null,
                StepId,
                SemanticDelta,
                ParentStepId,
                ReplayLog);
        }
        catch (Exception ex)
        {
            return FailWithCurrentTrace<TValue>(ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TNext> Map<TNext>(
        Func<TValue, TNext> mapper)
    {
        if (IsFailure)
            return PropagateFailure<TNext>();

        try
        {
            return new ResultStep<TState, TNext>(
                true,
                State,
                mapper(Value!),
                null,
                StepId,
                SemanticDelta,
                ParentStepId,
                ReplayLog);
        }
        catch (Exception ex)
        {
            return FailWithCurrentTrace<TNext>(ErrorContext.FromException(ex));
        }
    }

    public async Task<ResultStep<TState, TNext>> BindAsync<TNext>(
        Func<TValue, Task<ResultStep<TState, TNext>>> binder)
    {
        if (IsFailure)
            return PropagateFailure<TNext>();

        try
        {
            var next = await binder(Value!).ConfigureAwait(false);
            return next.WithParentStepId(StepId, ReplayLog);
        }
        catch (Exception ex)
        {
            return FailWithCurrentTrace<TNext>(ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TNext> Bind<TNext>(
        Func<TValue, ResultStep<TState, TNext>> binder)
    {
        if (IsFailure)
            return PropagateFailure<TNext>();

        try
        {
            return binder(Value!).WithParentStepId(StepId, ReplayLog);
        }
        catch (Exception ex)
        {
            return FailWithCurrentTrace<TNext>(ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TValue> Tap(
        Action<TValue> action)
    {
        if (IsFailure)
            return this;

        try
        {
            action(Value!);
            return this;
        }
        catch (Exception ex)
        {
            return FailWithCurrentTrace<TValue>(ErrorContext.FromException(ex));
        }
    }

    public Result<TValue> ToResult()
        => IsSuccess
            ? Result<TValue>.Success(Value!)
            : Result<TValue>.Fail(Error!);

    public ResultStep<TState, TNext> Select<TNext>(
        Func<TValue, TNext> selector)
        => Map(selector);

    public ResultStep<TState, TResult> SelectMany<TNext, TResult>(
        Func<TValue, ResultStep<TState, TNext>> binder,
        Func<TValue, TNext, TResult> projector)
        => Bind(value => binder(value).Map(next => projector(value, next)));

    public async Task<ResultStep<TState, TResult>> SelectMany<TNext, TResult>(
        Func<TValue, Task<ResultStep<TState, TNext>>> binder,
        Func<TValue, TNext, TResult> projector)
        => await BindAsync(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

    private ResultStep<TState, TNext> PropagateFailure<TNext>()
        => new(
            false,
            State,
            default,
            Error!,
            StepId,
            SemanticDelta,
            ParentStepId,
            ReplayLog);

    private ResultStep<TState, TNext> FailWithCurrentTrace<TNext>(
        ErrorContext error)
    {
        var replayLog = MarkCurrentReplayLogFailure(ReplayLog, error);
        var finalEntry = replayLog.Count > 0
            ? replayLog[^1]
            : new ResultStepReplayLogEntry(
                ResultStepIdentity.Create(
                    ParentStepId,
                    SemanticDelta,
                    isSuccess: false,
                    error.Code),
                ParentStepId,
                SemanticDelta,
                IsSuccess: false,
                error.Code);

        return new(
            false,
            State,
            default,
            error,
            finalEntry.StepId,
            finalEntry.SemanticDelta,
            finalEntry.ParentStepId,
            replayLog);
    }

    private ResultStep<TState, TValue> WithParentStepId(
        string parentStepId,
        IReadOnlyList<ResultStepReplayLogEntry> replayLogPrefix)
    {
        var rebasedReplayLog = RebaseReplayLog(ReplayLog, parentStepId);
        var finalReplayLog = ConcatReplayLogs(replayLogPrefix, rebasedReplayLog);
        var finalEntry = rebasedReplayLog.Count > 0
            ? rebasedReplayLog[^1]
            : new ResultStepReplayLogEntry(
                ResultStepIdentity.Create(
                    parentStepId,
                    SemanticDelta,
                    IsSuccess,
                    Error?.Code),
                parentStepId,
                SemanticDelta,
                IsSuccess,
                Error?.Code);

        return new(
            IsSuccess,
            State,
            Value,
            Error,
            finalEntry.StepId,
            finalEntry.SemanticDelta,
            finalEntry.ParentStepId,
            finalReplayLog);
    }

    private static IReadOnlyList<ResultStepReplayLogEntry> AppendReplayLogEntry(
        IReadOnlyList<ResultStepReplayLogEntry> replayLog,
        ResultStepReplayLogEntry entry)
    {
        var entries = new ResultStepReplayLogEntry[replayLog.Count + 1];

        for (var i = 0; i < replayLog.Count; i++)
        {
            entries[i] = replayLog[i];
        }

        entries[^1] = entry;
        return entries;
    }

    private static IReadOnlyList<ResultStepReplayLogEntry> ConcatReplayLogs(
        IReadOnlyList<ResultStepReplayLogEntry> first,
        IReadOnlyList<ResultStepReplayLogEntry> second)
    {
        if (first.Count == 0)
            return second;

        if (second.Count == 0)
            return first;

        var entries = new ResultStepReplayLogEntry[first.Count + second.Count];

        for (var i = 0; i < first.Count; i++)
        {
            entries[i] = first[i];
        }

        for (var i = 0; i < second.Count; i++)
        {
            entries[first.Count + i] = second[i];
        }

        return entries;
    }

    private static IReadOnlyList<ResultStepReplayLogEntry> RebaseReplayLog(
        IReadOnlyList<ResultStepReplayLogEntry> replayLog,
        string parentStepId)
    {
        if (replayLog.Count == 0)
            return Array.Empty<ResultStepReplayLogEntry>();

        var entries = new ResultStepReplayLogEntry[replayLog.Count];
        var currentParentStepId = parentStepId;

        for (var i = 0; i < replayLog.Count; i++)
        {
            var entry = replayLog[i];
            var stepId = ResultStepIdentity.Create(
                currentParentStepId,
                entry.SemanticDelta,
                entry.IsSuccess,
                entry.ErrorCode);
            entries[i] = entry with
            {
                StepId = stepId,
                ParentStepId = currentParentStepId
            };
            currentParentStepId = stepId;
        }

        return entries;
    }

    private static IReadOnlyList<ResultStepReplayLogEntry> MarkCurrentReplayLogFailure(
        IReadOnlyList<ResultStepReplayLogEntry> replayLog,
        ErrorContext error)
    {
        if (replayLog.Count == 0)
        {
            var stepId = ResultStepIdentity.Create(
                parentStepId: null,
                SemanticDelta.Empty,
                isSuccess: false,
                error.Code);

            return [
                new ResultStepReplayLogEntry(
                    stepId,
                    ParentStepId: null,
                    SemanticDelta.Empty,
                    IsSuccess: false,
                    error.Code)
            ];
        }

        var entries = new ResultStepReplayLogEntry[replayLog.Count];

        for (var i = 0; i < replayLog.Count; i++)
        {
            entries[i] = replayLog[i];
        }

        var current = replayLog[^1];
        entries[^1] = current with
        {
            StepId = ResultStepIdentity.Create(
                current.ParentStepId,
                current.SemanticDelta,
                isSuccess: false,
                error.Code),
            IsSuccess = false,
            ErrorCode = error.Code
        };

        return entries;
    }
}
