namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: TaskOptionExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskOptionExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskOptionExtensions']/summary" />
public static class TaskOptionExtensions
{
    /// <summary>EN: Documentation for public API. JA: AsTask&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.AsTask&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.AsTask&lt;T&gt;']/summary" />
    public static Task<Option<T>> AsTask<T>(
        this Option<T> option)
        => Task.FromResult(option);

    /// <summary>EN: Documentation for public API. JA: Tap&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Tap&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Tap&lt;T&gt;']/summary" />
    public static async Task<Option<T>> Tap<T>(
        this Task<Option<T>> task,
        Action<T> action)
    {
        var option = await task.ConfigureAwait(false);
        return option.Tap(action);
    }

    /// <summary>EN: Documentation for public API. JA: Tap&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Tap&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Tap&lt;T&gt;']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.V&gt;']/summary" />
    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Option<T> option,
        Func<T, Task<Option<U>>> binder,
        Func<T, U, V> projector)
        => await option
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>EN: Documentation for public API. JA: U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    public static async Task<Option<U>> Map<T, U>(
        this Task<Option<T>> task,
        Func<T, U> selector)
    {
        var opt = await task.ConfigureAwait(false);
        return opt.Map(selector);
    }

    /// <summary>EN: Documentation for public API. JA: U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    public static Task<Option<U>> Select<T, U>(
        this Task<Option<T>> task,
        Func<T, U> selector)
        => task.Map(selector);

    /// <summary>EN: Documentation for public API. JA: U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    public static async Task<Option<U>> Bind<T, U>(
        this Option<T> option,
        Func<T, Task<Option<U>>> binder)
    {
        if (!option.HasValue)
            return Option<U>.None();

        return await binder(option.Value!).ConfigureAwait(false);
    }

    /// <summary>EN: Documentation for public API. JA: U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    public static async Task<Option<U>> Bind<T, U>(
        this Task<Option<T>> task,
        Func<T, Task<Option<U>>> binder)
    {
        var option = await task.ConfigureAwait(false);
        if (!option.HasValue)
            return Option<U>.None();

        return await binder(option.Value!).ConfigureAwait(false);
    }

    /// <summary>EN: Documentation for public API. JA: U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.U&gt;']/summary" />
    public static async Task<Option<U>> Bind<T, U>(
        this Task<Option<T>> task,
        Func<T, Option<U>> binder)
    {
        var option = await task.ConfigureAwait(false);
        return option.Bind(binder);
    }

    /// <summary>EN: Documentation for public API. JA: V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.V&gt;']/summary" />
    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Task<Option<T>> task,
        Func<T, Task<Option<U>>> binder,
        Func<T, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>EN: Documentation for public API. JA: V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.V&gt;']/summary" />
    public static async Task<Option<V>> SelectMany<T, U, V>(
        this Task<Option<T>> task,
        Func<T, Option<U>> binder,
        Func<T, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>EN: Documentation for public API. JA: Where&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Where&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Where&lt;T&gt;']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: Where&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Where&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskOptionExtensions.Where&lt;T&gt;']/summary" />
    public static async Task<Option<T>> Where<T>(
        this Task<Option<T>> task,
        Func<T, Task<bool>> predicate)
    {
        var option = await task.ConfigureAwait(false);
        if (!option.HasValue)
            return option;

        return await predicate(option.Value!).ConfigureAwait(false)
            ? option
            : Option<T>.None();
    }
}
