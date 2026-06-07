namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskEitherExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskEitherExtensions']" />
public static class TaskEitherExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    public static Task<Either<L, R>> AsTask<L, R>(
        this Either<L, R> either)
        => Task.FromResult(either);

    /// <summary>Executes the Tap&lt;L, R&gt; operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで Tap&lt;L, R&gt; 操作を実行します。</summary>
    public static async Task<Either<L, R>> Tap<L, R>(
        this Task<Either<L, R>> task,
        Action<R> action)
    {
        var either = await task.ConfigureAwait(false);
        return either.Tap(action);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']" />
    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
        => await either
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    public static async Task<Either<L, U>> Map<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, U> selector)
    {
        var e = await task.ConfigureAwait(false);
        return e.Map(selector);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    public static Task<Either<L, U>> Select<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, U> selector)
        => task.Map(selector);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder)
    {
        if (either.IsLeft)
            return Either<L, U>.FromLeft(either.Left!);

        return await binder(either.Right!).ConfigureAwait(false);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder)
    {
        var either = await task.ConfigureAwait(false);
        if (either.IsLeft)
            return Either<L, U>.FromLeft(either.Left!);

        return await binder(either.Right!).ConfigureAwait(false);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']" />
    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder)
    {
        var either = await task.ConfigureAwait(false);
        return either.Bind(binder);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']" />
    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']" />
    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    public static async Task<Either<L, R>> Where<L, R>(
        this Task<Either<L, R>> task,
        Func<R, bool> predicate,
        Func<L> leftFactory)
    {
        var either = await task.ConfigureAwait(false);
        if (either.IsLeft)
            return either;

        return predicate(either.Right!)
            ? either
            : Either<L, R>.FromLeft(leftFactory());
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']" />
    public static async Task<Either<L, R>> Where<L, R>(
        this Task<Either<L, R>> task,
        Func<R, Task<bool>> predicate,
        Func<L> leftFactory)
    {
        var either = await task.ConfigureAwait(false);
        if (either.IsLeft)
            return either;

        return await predicate(either.Right!).ConfigureAwait(false)
            ? either
            : Either<L, R>.FromLeft(leftFactory());
    }
}
