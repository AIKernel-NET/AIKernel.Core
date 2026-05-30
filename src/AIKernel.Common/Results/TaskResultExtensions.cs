using System;
using System.Threading.Tasks;

namespace AIKernel.Common.Results;

public static class TaskResultExtensions
{
    // -------------------------
    // Map（Select）
    // -------------------------
    public static async Task<Result<U>> Select<T, U>(
        this Task<Result<T>> task,
        Func<T, U> selector)
    {
        try
        {
            var r = await task.ConfigureAwait(false);
            if (r.IsFailure)
                return Result<U>.Fail(r.Error!);

            return Result<U>.Success(selector(r.Value!));
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    // -------------------------
    // Bind（SelectMany）
    // -------------------------
    public static async Task<Result<V>> SelectMany<T, U, V>(
        this Task<Result<T>> task,
        Func<T, Task<Result<U>>> binder,
        Func<T, U, V> projector)
    {
        try
        {
            var r1 = await task.ConfigureAwait(false);
            if (r1.IsFailure)
                return Result<V>.Fail(r1.Error!);

            var r2 = await binder(r1.Value!).ConfigureAwait(false);
            if (r2.IsFailure)
                return Result<V>.Fail(r2.Error!);

            return Result<V>.Success(projector(r1.Value!, r2.Value!));
        }
        catch (Exception ex)
        {
            return Result<V>.Fail(ErrorContext.FromException(ex));
        }
    }
}
