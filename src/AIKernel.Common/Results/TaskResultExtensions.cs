using System;
using System.Threading.Tasks;

namespace AIKernel.Common.Results;

/// <summary>[EN] Documents this public package API member. [JA] TaskResultExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultExtensions']/summary" />
public static class TaskResultExtensions
{
    /// <summary>[EN] Documents this public package API member. [JA] AsTask&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.AsTask&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.AsTask&lt;T&gt;']/summary" />
    public static Task<Result<T>> AsTask<T>(
        this Result<T> result)
        => Task.FromResult(result);

    /// <summary>[EN] Documents this public package API member. [JA] Tap&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.Tap&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.Tap&lt;T&gt;']/summary" />
    public static async Task<Result<T>> Tap<T>(
        this Task<Result<T>> task,
        Action<T> action)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            return result.Tap(action);
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>[EN] Documents this public package API member. [JA] Tap&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.Tap&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.Tap&lt;T&gt;']/summary" />
    public static async Task<Result<T>> Tap<T>(
        this Task<Result<T>> task,
        Func<T, Task> action)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            return await result
                .Bind(async value =>
                {
                    await action(value).ConfigureAwait(false);
                    return Result<T>.Success(value);
                })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ErrorContext.FromException(ex));
        }
    }

    // -------------------------
    // Map（Select）
    // -------------------------
    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    public static async Task<Result<U>> Map<T, U>(
        this Task<Result<T>> task,
        Func<T, U> selector)
    {
        try
        {
            var r = await task.ConfigureAwait(false);
            return r.Map(selector);
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    // -------------------------
    // Bind（SelectMany）
    // -------------------------
    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    public static async Task<Result<U>> Bind<T, U>(
        this Result<T> result,
        Func<T, Task<Result<U>>> binder)
    {
        if (result.IsFailure)
            return Result<U>.Fail(result.Error!);

        try
        {
            return await binder(result.Value!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    public static Task<Result<U>> Select<T, U>(
        this Task<Result<T>> task,
        Func<T, U> selector)
        => task.Map(selector);

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    public static async Task<Result<U>> Bind<T, U>(
        this Task<Result<T>> task,
        Func<T, Task<Result<U>>> binder)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            if (result.IsFailure)
                return Result<U>.Fail(result.Error!);

            return await binder(result.Value!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.U&gt;']/summary" />
    public static async Task<Result<U>> Bind<T, U>(
        this Task<Result<T>> task,
        Func<T, Result<U>> binder)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            return result.Bind(binder);
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>[EN] Documents this public package API member. [JA] V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.V&gt;']/summary" />
    public static async Task<Result<V>> SelectMany<T, U, V>(
        this Result<T> result,
        Func<T, Task<Result<U>>> binder,
        Func<T, U, V> projector)
        => await result
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>[EN] Documents this public package API member. [JA] V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.V&gt;']/summary" />
    public static async Task<Result<V>> SelectMany<T, U, V>(
        this Task<Result<T>> task,
        Func<T, Task<Result<U>>> binder,
        Func<T, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);

    /// <summary>[EN] Documents this public package API member. [JA] V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultExtensions.V&gt;']/summary" />
    public static async Task<Result<V>> SelectMany<T, U, V>(
        this Task<Result<T>> task,
        Func<T, Result<U>> binder,
        Func<T, U, V> projector)
        => await task
            .Bind(value => binder(value).Map(bound => projector(value, bound)))
            .ConfigureAwait(false);
}
