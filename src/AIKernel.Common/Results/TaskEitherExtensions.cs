namespace AIKernel.Common.Results;

public static class TaskEitherExtensions
{
    public static async Task<Either<L, R>> Tap<L, R>(
        this Task<Either<L, R>> task,
        Action<R> action)
    {
        var either = await task;
        return either.Tap(action);
    }

    public static async Task<Either<L, R>> Tap<L, R>(
        this Task<Either<L, R>> task,
        Func<R, Task> action)
    {
        var either = await task;
        if (either.IsLeft)
            return either;

        await action(either.Right!);
        return either;
    }

    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
    {
        if (either.IsLeft)
            return Either<L, V>.FromLeft(either.Left!);

        var next = await binder(either.Right!);
        if (next.IsLeft)
            return Either<L, V>.FromLeft(next.Left!);

        return Either<L, V>.FromRight(projector(either.Right!, next.Right!));
    }

    public static async Task<Either<L, U>> Select<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, U> selector)
    {
        var e = await task;
        return e.IsRight
            ? Either<L, U>.FromRight(selector(e.Right!))
            : Either<L, U>.FromLeft(e.Left!);
    }

    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
    {
        var e1 = await task;
        if (e1.IsLeft)
            return Either<L, V>.FromLeft(e1.Left!);

        var e2 = await binder(e1.Right!);
        if (e2.IsLeft)
            return Either<L, V>.FromLeft(e2.Left!);

        return Either<L, V>.FromRight(projector(e1.Right!, e2.Right!));
    }

    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
    {
        var e1 = await task;
        if (e1.IsLeft)
            return Either<L, V>.FromLeft(e1.Left!);

        var e2 = binder(e1.Right!);
        if (e2.IsLeft)
            return Either<L, V>.FromLeft(e2.Left!);

        return Either<L, V>.FromRight(projector(e1.Right!, e2.Right!));
    }
}
