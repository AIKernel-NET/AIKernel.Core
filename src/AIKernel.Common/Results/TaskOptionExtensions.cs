namespace AIKernel.Common.Results;

public static class TaskOptionExtensions
{
    public static async Task<Option<U>> Select<T, U>(
        this Task<Option<T>> task,
        Func<T, U> selector)
    {
        var opt = await task;
        return opt.Map(selector);
    }

    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Task<Option<T>> task,
        Func<T, Task<Option<U>>> binder,
        Func<T, U, V> projector)
    {
        var o1 = await task;
        if (!o1.HasValue)
            return Option<V>.None();

        var o2 = await binder(o1.Value!);
        if (!o2.HasValue)
            return Option<V>.None();

        return Option<V>.Some(projector(o1.Value!, o2.Value!));
    }
}
