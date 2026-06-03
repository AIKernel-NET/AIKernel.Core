namespace AIKernel.Common.Results;

public static class TaskOptionExtensions
{
    public static async Task<Option<T>> Tap<T>(
        this Task<Option<T>> task,
        Action<T> action)
    {
        var option = await task.ConfigureAwait(false);
        return option.Tap(action);
    }

    public static async Task<Option<T>> Tap<T>(
        this Task<Option<T>> task,
        Func<T, Task> action)
    {
        var option = await task.ConfigureAwait(false);
        if (!option.HasValue)
            return option;

        await action(option.Value!).ConfigureAwait(false);
        return option;
    }

    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Option<T> option,
        Func<T, Task<Option<U>>> binder,
        Func<T, U, V> projector)
        => await option
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    public static async Task<Option<U>> Map<T, U>(
        this Task<Option<T>> task,
        Func<T, U> selector)
    {
        var opt = await task.ConfigureAwait(false);
        return opt.Map(selector);
    }

    public static Task<Option<U>> Select<T, U>(
        this Task<Option<T>> task,
        Func<T, U> selector)
        => task.Map(selector);

    public static async Task<Option<U>> Bind<T, U>(
        this Option<T> option,
        Func<T, Task<Option<U>>> binder)
    {
        if (!option.HasValue)
            return Option<U>.None();

        return await binder(option.Value!).ConfigureAwait(false);
    }

    public static async Task<Option<U>> Bind<T, U>(
        this Task<Option<T>> task,
        Func<T, Task<Option<U>>> binder)
    {
        var option = await task.ConfigureAwait(false);
        if (!option.HasValue)
            return Option<U>.None();

        return await binder(option.Value!).ConfigureAwait(false);
    }

    public static async Task<Option<U>> Bind<T, U>(
        this Task<Option<T>> task,
        Func<T, Option<U>> binder)
    {
        var option = await task.ConfigureAwait(false);
        return option.Bind(binder);
    }

    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Task<Option<T>> task,
        Func<T, Task<Option<U>>> binder,
        Func<T, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Task<Option<T>> task,
        Func<T, Option<U>> binder,
        Func<T, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    public static async Task<Option<T>> Where<T>(
        this Task<Option<T>> task,
        Func<T, bool> predicate)
    {
        var option = await task.ConfigureAwait(false);
        if (!option.HasValue)
            return option;

        return predicate(option.Value!)
            ? option
            : Option<T>.None();
    }
}
