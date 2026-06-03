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

    private ResultStep(
        bool isSuccess,
        TState state,
        TValue? value,
        ErrorContext? error,
        string? stepId,
        SemanticDelta? semanticDelta)
    {
        IsSuccess = isSuccess;
        State = state;
        Value = value;
        Error = error;
        SemanticDelta = semanticDelta ?? SemanticDelta.Empty;
        StepId = string.IsNullOrWhiteSpace(stepId)
            ? ResultStepIdentity.Create(
                parentStepId: null,
                SemanticDelta,
                IsSuccess,
                Error?.Code)
            : stepId;
    }

    public static ResultStep<TState, TValue> Success(
        TState state,
        TValue value)
        => new(true, state, value, null, stepId: null, semanticDelta: null);

    public static ResultStep<TState, TValue> Fail(
        TState state,
        ErrorContext error)
        => new(false, state, default, error, stepId: null, semanticDelta: null);

    public static ResultStep<TState, TValue> FromResult(
        TState state,
        Result<TValue> result)
        => result.IsSuccess
            ? Success(state, result.Value!)
            : Fail(state, result.Error!);

    public ResultStep<TState, TValue> WithState(TState state)
        => IsSuccess
            ? new(true, state, Value!, null, StepId, SemanticDelta)
            : new(false, state, default, Error!, StepId, SemanticDelta);

    public ResultStep<TState, TValue> WithSemanticDelta(
        SemanticDelta semanticDelta,
        string? parentStepId = null)
        => new(
            IsSuccess,
            State,
            Value,
            Error,
            ResultStepIdentity.Create(
                parentStepId ?? StepId,
                semanticDelta,
                IsSuccess,
                Error?.Code),
            semanticDelta);

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
                SemanticDelta);
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
            return FailWithCurrentTrace<TNext>(Error!);

        try
        {
            return ResultStep<TState, TNext>.Success(State, mapper(Value!))
                .WithSemanticDelta(SemanticDelta, StepId);
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
            return FailWithCurrentTrace<TNext>(Error!);

        try
        {
            var next = await binder(Value!).ConfigureAwait(false);
            return next.WithParentStepId(StepId);
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
            return FailWithCurrentTrace<TNext>(Error!);

        try
        {
            return binder(Value!).WithParentStepId(StepId);
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
        => await BindAsync(value => binder(value).Select(next => projector(value, next)))
            .ConfigureAwait(false);

    private ResultStep<TState, TNext> FailWithCurrentTrace<TNext>(
        ErrorContext error)
        => ResultStep<TState, TNext>
            .Fail(State, error)
            .WithSemanticDelta(SemanticDelta, StepId);

    private ResultStep<TState, TValue> WithParentStepId(string parentStepId)
        => new(
            IsSuccess,
            State,
            Value,
            Error,
            ResultStepIdentity.Create(
                parentStepId,
                SemanticDelta,
                IsSuccess,
                Error?.Code),
            SemanticDelta);
}
