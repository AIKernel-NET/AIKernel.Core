namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: ResultStep を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultStep']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultStep']/summary" />
public readonly struct ResultStep<TState, TValue>
{
    /// <summary>EN: Documentation for public API. JA: IsSuccess を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.IsSuccess']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.IsSuccess']/summary" />
    public bool IsSuccess { get; }

    /// <summary>EN: Documentation for public API. JA: IsFailure を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.ResultStep.IsFailure']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.ResultStep.IsFailure']/summary" />
    public bool IsFailure => !IsSuccess;

    /// <summary>EN: Documentation for public API. JA: IsSuspended を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.ResultStep.IsSuspended']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.ResultStep.IsSuspended']/summary" />
    public bool IsSuspended => Error?.Code == PipelineStep.SuspendErrorCode;

    /// <summary>EN: Documentation for public API. JA: State を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.State']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.State']/summary" />
    public TState State { get; }

    /// <summary>EN: Documentation for public API. JA: Value を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.Value']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.Value']/summary" />
    public TValue? Value { get; }

    /// <summary>EN: Documentation for public API. JA: Error を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.Error']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.Error']/summary" />
    public ErrorContext? Error { get; }

    /// <summary>EN: Documentation for public API. JA: StepId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.StepId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.StepId']/summary" />
    public string StepId { get; }

    /// <summary>EN: Documentation for public API. JA: SemanticDelta を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.SemanticDelta']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.SemanticDelta']/summary" />
    public SemanticDelta SemanticDelta { get; }

    /// <summary>EN: Documentation for public API. JA: ParentStepId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.ParentStepId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.ParentStepId']/summary" />
    public string? ParentStepId { get; }

    /// <summary>EN: Documentation for public API. JA: ReplayLog を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.ReplayLog']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.ReplayLog']/summary" />
    public IReadOnlyList<ResultStepReplayLogEntry> ReplayLog { get; }

    /// <summary>EN: Documentation for public API. JA: ReplayLogHash を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.ReplayLogHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ResultStep.ReplayLogHash']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: Success を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Success']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Success']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: Fail を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Fail']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Fail']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: FromResult を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.FromResult']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.FromResult']/summary" />
    public static ResultStep<TState, TValue> FromResult(
        TState state,
        Result<TValue> result)
        => result.IsSuccess
            ? Success(state, result.Value!)
            : Fail(state, result.Error!);

    /// <summary>EN: Documentation for public API. JA: WithState を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.WithState']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.WithState']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: WithSemanticDelta を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.WithSemanticDelta']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.WithSemanticDelta']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: MapState を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.MapState']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.MapState']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: Map&lt;TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Map&lt;TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Map&lt;TNext&gt;']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: BindAsync&lt;TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.BindAsync&lt;TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.BindAsync&lt;TNext&gt;']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: Bind&lt;TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Bind&lt;TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Bind&lt;TNext&gt;']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: Tap を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Tap']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Tap']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: ToResult を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.ToResult']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.ToResult']/summary" />
    public Result<TValue> ToResult()
        => IsSuccess
            ? Result<TValue>.Success(Value!)
            : Result<TValue>.Fail(Error!);

    /// <summary>
    /// [EN] Projects the pipeline step through either the fail-closed branch or the success branch.
    /// [JA] fail-closed branch または success branch を通じて pipeline step を射影します。
    /// </summary>
    public TMatch Match<TMatch>(
        Func<TState, ErrorContext, TMatch> failFunc,
        Func<TState, TValue, TMatch> successFunc)
    {
        ArgumentNullException.ThrowIfNull(failFunc);
        ArgumentNullException.ThrowIfNull(successFunc);
        return IsSuccess
            ? successFunc(State, Value!)
            : failFunc(State, Error!);
    }

    /// <summary>EN: Documentation for public API. JA: WithReplayLogPrefix を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.WithReplayLogPrefix']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.WithReplayLogPrefix']/summary" />
    public ResultStep<TState, TValue> WithReplayLogPrefix(
        IReadOnlyList<ResultStepReplayLogEntry> replayLogPrefix)
    {
        ArgumentNullException.ThrowIfNull(replayLogPrefix);

        return replayLogPrefix.Count == 0
            ? this
            : WithParentStepId(replayLogPrefix[^1].StepId, replayLogPrefix);
    }

    /// <summary>EN: Documentation for public API. JA: Select&lt;TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Select&lt;TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.Select&lt;TNext&gt;']/summary" />
    public ResultStep<TState, TNext> Select<TNext>(
        Func<TValue, TNext> selector)
        => Map(selector);

    /// <summary>EN: Documentation for public API. JA: TResult&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.TResult&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.TResult&gt;']/summary" />
    public ResultStep<TState, TResult> SelectMany<TNext, TResult>(
        Func<TValue, ResultStep<TState, TNext>> binder,
        Func<TValue, TNext, TResult> projector)
        => Bind(value => binder(value).Map(next => projector(value, next)));

    /// <summary>EN: Documentation for public API. JA: TResult&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.TResult&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultStep.TResult&gt;']/summary" />
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
