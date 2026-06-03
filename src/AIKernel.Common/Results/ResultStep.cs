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
        Func<TState, TValue, TState> mapper)
    {
        if (IsFailure)
            return this;

        try
        {
            return Success(mapper(State, Value!), Value!);
        }
        catch (Exception ex)
        {
            return Fail(State, ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TNext> Map<TNext>(
        Func<TValue, TNext> mapper)
    {
        if (IsFailure)
            return ResultStep<TState, TNext>.Fail(State, Error!);

        try
        {
            return ResultStep<TState, TNext>.Success(State, mapper(Value!));
        }
        catch (Exception ex)
        {
            return ResultStep<TState, TNext>.Fail(State, ErrorContext.FromException(ex));
        }
    }

    public async Task<ResultStep<TState, TNext>> BindAsync<TNext>(
        Func<TValue, Task<ResultStep<TState, TNext>>> binder)
    {
        if (IsFailure)
            return ResultStep<TState, TNext>.Fail(State, Error!);

        try
        {
            return await binder(Value!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ResultStep<TState, TNext>.Fail(State, ErrorContext.FromException(ex));
        }
    }

    public ResultStep<TState, TNext> Bind<TNext>(
        Func<TValue, ResultStep<TState, TNext>> binder)
    {
        if (IsFailure)
            return ResultStep<TState, TNext>.Fail(State, Error!);

        try
        {
            return binder(Value!);
        }
        catch (Exception ex)
        {
            return ResultStep<TState, TNext>.Fail(State, ErrorContext.FromException(ex));
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
            return Fail(State, ErrorContext.FromException(ex));
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
}
