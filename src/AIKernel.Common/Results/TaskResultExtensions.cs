namespace AIKernel.Common.Results;

public static class TaskResultExtensions
{
    public static async Task<Result<U>> Select<T, U>(
        this Task<Result<T>> task,
        Func<T, U> selector)
    {
        var result = await task;
        return result.Map(selector);
    }

    public static async Task<Result<V>> SelectMany<T, U, V>(
        this Task<Result<T>> task,
        Func<T, Task<Result<U>>> binder,
        Func<T, U, V> projector)
    {
        var r1 = await task;
        if (r1.IsFailure)
            return Result<V>.Fail(r1.Error!);

        var r2 = await binder(r1.Value!);
        if (r2.IsFailure)
            return Result<V>.Fail(r2.Error!);

        return Result<V>.Success(projector(r1.Value!, r2.Value!));
    }
}
