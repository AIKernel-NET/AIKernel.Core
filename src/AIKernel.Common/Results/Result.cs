using System.Diagnostics.CodeAnalysis;

namespace AIKernel.Common.Results;

public readonly struct Result<T>
{
    // -------------------------
    // Core State
    // -------------------------

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ErrorContext? Error { get; }

    // Nullable Flow Analysis:
    // IsSuccess = true → Value は null ではない
    // IsSuccess = false → Error は null ではない
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessState => IsSuccess;

    private Result(bool success, T? value, ErrorContext? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    // -------------------------
    // Constructors
    // -------------------------

    public static Result<T> Success(T value)
        => new(true, value, null);

    public static Result<T> Fail(string message)
        => new(false, default, new ErrorContext(message, "ERROR", false));

    public static Result<T> Fail(ErrorContext error)
        => new(false, default, error);

    // -------------------------
    // Functional Extensions
    // -------------------------

    public Result<U> Map<U>(Func<T, U> mapper)
    {
        if (IsFailure)
            return Result<U>.Fail(Error!);

        try
        {
            return Result<U>.Success(mapper(Value!));
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    public Result<U> Bind<U>(Func<T, Result<U>> binder)
    {
        if (IsFailure)
            return Result<U>.Fail(Error!);

        try
        {
            return binder(Value!);
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

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

        try
        {
            var r = binder(Value!);
            if (r.IsFailure)
                return Result<V>.Fail(r.Error!);

            return Result<V>.Success(projector(Value!, r.Value!));
        }
        catch (Exception ex)
        {
            return Result<V>.Fail(ErrorContext.FromException(ex));
        }
    }

    public override string ToString()
        => IsSuccess ? $"Success({Value})" : $"Fail({Error})";
}
