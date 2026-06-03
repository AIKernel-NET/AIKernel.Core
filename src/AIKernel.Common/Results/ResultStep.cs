namespace AIKernel.Common.Results;

public readonly struct ResultStep<TState, TValue>
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public TState State { get; }

    public TValue? Value { get; }

    public ErrorContext? Error { get; }

    private ResultStep(
        bool isSuccess,
        TState state,
        TValue? value,
        ErrorContext? error)
    {
        IsSuccess = isSuccess;
        State = state;
        Value = value;
        Error = error;
    }

    public static ResultStep<TState, TValue> Success(
        TState state,
        TValue value)
        => new(true, state, value, null);

    public static ResultStep<TState, TValue> Fail(
        TState state,
        ErrorContext error)
        => new(false, state, default, error);

    public static ResultStep<TState, TValue> FromResult(
        TState state,
        Result<TValue> result)
        => result.IsSuccess
            ? Success(state, result.Value!)
            : Fail(state, result.Error!);

    public ResultStep<TState, TValue> WithState(TState state)
        => IsSuccess
            ? Success(state, Value!)
            : Fail(state, Error!);

    public ResultStep<TState, TValue> MapState(
        Func<ResultStep<TState, TValue>, TState> mapper)
    {
        if (IsFailure)
            return this;

        try
        {
            return Success(mapper(this), Value!);
        }
        catch (Exception ex)
        {
            return Fail(State, ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TNext> Map<TNext>(
        Func<ResultStep<TState, TValue>, TNext> mapper)
    {
        if (IsFailure)
            return ResultStep<TState, TNext>.Fail(State, Error!);

        try
        {
            return ResultStep<TState, TNext>.Success(State, mapper(this));
        }
        catch (Exception ex)
        {
            return ResultStep<TState, TNext>.Fail(State, ErrorContext.FromException(ex));
        }
    }

    public async Task<ResultStep<TState, TNext>> BindAsync<TNext>(
        Func<ResultStep<TState, TValue>, Task<ResultStep<TState, TNext>>> binder)
    {
        if (IsFailure)
            return ResultStep<TState, TNext>.Fail(State, Error!);

        try
        {
            return await binder(this).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ResultStep<TState, TNext>.Fail(State, ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TNext> Bind<TNext>(
        Func<ResultStep<TState, TValue>, ResultStep<TState, TNext>> binder)
    {
        if (IsFailure)
            return ResultStep<TState, TNext>.Fail(State, Error!);

        try
        {
            return binder(this);
        }
        catch (Exception ex)
        {
            return ResultStep<TState, TNext>.Fail(State, ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TValue> Tap(
        Action<ResultStep<TState, TValue>> action)
    {
        if (IsFailure)
            return this;

        try
        {
            action(this);
            return this;
        }
        catch (Exception ex)
        {
            return Fail(State, ErrorContext.FromException(ex));
        }
    }

    public Result<TValue> ToResult()
        => IsSuccess
            ? Result<TValue>.Success(Value!)
            : Result<TValue>.Fail(Error!);

    public ResultStep<TState, TNext> Select<TNext>(
        Func<ResultStep<TState, TValue>, TNext> selector)
        => Map(selector);

    public ResultStep<TState, TResult> SelectMany<TNext, TResult>(
        Func<ResultStep<TState, TValue>, ResultStep<TState, TNext>> binder,
        Func<ResultStep<TState, TValue>, ResultStep<TState, TNext>, TResult> projector)
        => Bind(step => binder(step).Map(next => projector(step, next)));
}
