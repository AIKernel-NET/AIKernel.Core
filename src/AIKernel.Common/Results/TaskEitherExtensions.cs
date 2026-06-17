namespace AIKernel.Common.Results;

/// <summary>[EN] Documents this public package API member. [JA] TaskEitherExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskEitherExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskEitherExtensions']/summary" />
public static class TaskEitherExtensions
{
    /// <summary>[EN] Documents this public package API member. [JA] R&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
    public static Task<Either<L, R>> AsTask<L, R>(
        this Either<L, R> either)
        => Task.FromResult(either);

    /// <summary>
    /// [EN] Runs a side-effect callback for a successful task-wrapped either value.
    /// [JA] task で包まれた either の成功値に対して side-effect callback を実行します。
    /// </summary>
    public static async Task<Either<L, R>> Tap<L, R>(
        this Task<Either<L, R>> task,
        Action<R> action)
    {
        var either = await task.ConfigureAwait(false);
        return either.Tap(action);
    }

    /// <summary>[EN] Documents this public package API member. [JA] R&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']/summary" />
    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
        => await either
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    public static async Task<Either<L, U>> Map<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, U> selector)
    {
        var e = await task.ConfigureAwait(false);
        return e.Map(selector);
    }

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    public static Task<Either<L, U>> Select<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, U> selector)
        => task.Map(selector);

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Either<L, R> either,
        Func<R, Task<Either<L, U>>> binder)
    {
        if (either.IsLeft)
            return Either<L, U>.FromLeft(either.Left!);

        return await binder(either.Right!).ConfigureAwait(false);
    }

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder)
    {
        var either = await task.ConfigureAwait(false);
        if (either.IsLeft)
            return Either<L, U>.FromLeft(either.Left!);

        return await binder(either.Right!).ConfigureAwait(false);
    }

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.U&gt;']/summary" />
    public static async Task<Either<L, U>> Bind<L, R, U>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder)
    {
        var either = await task.ConfigureAwait(false);
        return either.Bind(binder);
    }

    /// <summary>[EN] Documents this public package API member. [JA] V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']/summary" />
    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Task<Either<L, U>>> binder,
        Func<R, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>[EN] Documents this public package API member. [JA] V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.V&gt;']/summary" />
    public static async Task<Either<L, V>> SelectMany<L, R, U, V>(
        this Task<Either<L, R>> task,
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>[EN] Documents this public package API member. [JA] R&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] R&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskEitherExtensions.R&gt;']/summary" />
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
