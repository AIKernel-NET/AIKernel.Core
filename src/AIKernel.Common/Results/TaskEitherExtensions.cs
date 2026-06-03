namespace AIKernel.Common.Results;

public static class TaskEitherExtensions
{
    public static async Task<Either<L, R>> Tap<L, R>(
        this Task<Either<L, R>> task,
        Action<R> action)
    {
        var either = await task.ConfigureAwait(false);
        return either.Tap(action);
    }

    public static async Task<Either<L, R>> Tap<L, R>(
        this Task<Either<L, R>> task,
        Func<R, Task> action)
    {
        var either = await task.ConfigureAwait(false);
        if (either.IsLeft)
            return either;

        await action(either.Right!).ConfigureAwait(false);
        return either;
    }

    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
        => await either
            .Bind(value => binder(value).Select(bound => projector(value, bound)))
            .ConfigureAwait(false);

    public static async Task<Either<L, U>> Select<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, U> selector)
    {
        var e = await task.ConfigureAwait(false);
        return e.IsRight
            ? Either<L, U>.FromRight(selector(e.Right!))
            : Either<L, U>.FromLeft(e.Left!);
    }

    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder)
    {
        if (either.IsLeft)
            return Either<L, U>.FromLeft(either.Left!);

        return await binder(either.Right!).ConfigureAwait(false);
    }

    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder)
    {
        var either = await task.ConfigureAwait(false);
        if (either.IsLeft)
            return Either<L, U>.FromLeft(either.Left!);

        return await binder(either.Right!).ConfigureAwait(false);
    }

    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder)
    {
        var either = await task.ConfigureAwait(false);
        return either.IsRight
            ? binder(either.Right!)
            : Either<L, U>.FromLeft(either.Left!);
    }

    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
        => await task
            .Bind(value => binder(value).Select(bound => projector(value, bound)))
            .ConfigureAwait(false);

    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);
}
