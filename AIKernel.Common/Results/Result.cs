namespace AIKernel.Common.Results;

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool success, T? value, string? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value)
        => new(true, value, null);

    public static Result<T> Fail(string error)
        => new(false, default, error);

    // -------------------------
    // Functional Extensions
    // -------------------------

    public Result<U> Map<U>(Func<T, U> mapper)
        => IsFailure
            ? Result<U>.Fail(Error!)
            : Result<U>.Success(mapper(Value!));

    public Result<U> Bind<U>(Func<T, Result<U>> binder)
        => IsFailure
            ? Result<U>.Fail(Error!)
            : binder(Value!);

    // -------------------------
    // LINQ Support
    // -------------------------

    public Result<U> Select<U>(Func<T, U> selector)
        => Map(selector);

    public Result<V> SelectMany<U, V>(
        Func<T, Result<U>> binder,
        Func<T, U, V> projector)
    {
        if (IsFailure)
            return Result<V>.Fail(Error!);

        var r = binder(Value!);
        if (r.IsFailure)
            return Result<V>.Fail(r.Error!);

        return Result<V>.Success(projector(Value!, r.Value!));
    }

    public override string ToString()
        => IsSuccess ? $"Success({Value})" : $"Fail({Error})";
}
