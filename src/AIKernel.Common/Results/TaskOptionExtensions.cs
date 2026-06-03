namespace AIKernel.Common.Results;

public static class TaskOptionExtensions
{
    public static async Task<Option<T>> Tap<T>(
        this Task<Option<T>> task,
        Action<T> action)
    {
        var option = await task;
        return option.Tap(action);
    }

    public static async Task<Option<T>> Tap<T>(
        this Task<Option<T>> task,
        Func<T, Task> action)
    {
        var option = await task;
        if (!option.HasValue)
            return option;

        await action(option.Value!);
        return option;
    }

    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Option<T> option,
        Func<T, Task<Option<U>>> binder,
        Func<T, U, V> projector)
    {
        if (!option.HasValue)
            return Option<V>.None();

        var next = await binder(option.Value!);
        if (!next.HasValue)
            return Option<V>.None();

        return Option<V>.Some(projector(option.Value!, next.Value!));
    }

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

    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Task<Option<T>> task,
        Func<T, Option<U>> binder,
        Func<T, U, V> projector)
    {
        var o1 = await task;
        if (!o1.HasValue)
            return Option<V>.None();

        var o2 = binder(o1.Value!);
        if (!o2.HasValue)
            return Option<V>.None();

        return Option<V>.Some(projector(o1.Value!, o2.Value!));
    }

    public static async Task<Option<T>> Where<T>(
        this Task<Option<T>> task,
        Func<T, bool> predicate)
    {
        var option = await task;
        if (!option.HasValue)
            return option;

        return predicate(option.Value!)
            ? option
            : Option<T>.None();
    }
}
