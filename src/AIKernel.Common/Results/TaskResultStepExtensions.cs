namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: TaskResultStepExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultStepExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultStepExtensions']/summary" />
public static class TaskResultStepExtensions
{
    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    public static Task<ResultStep<TState, TValue>> AsTask<TState, TValue>(
        this ResultStep<TState, TValue> step)
        => Task.FromResult(step);

    /// <summary>
    /// [EN] Runs a side-effect callback for a successful task-wrapped result step value.
    /// [JA] task で包まれた result step の成功値に対して side-effect callback を実行します。
    /// </summary>
    public static async Task<ResultStep<TState, TValue>> Tap<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Action<TValue> action)
    {
        var step = await task.ConfigureAwait(false);
        return step.Tap(action);
    }

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    public static async Task<ResultStep<TState, TValue>> Tap<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task> action)
    {
        var step = await task.ConfigureAwait(false);
        if (step.IsFailure)
            return step;

        try
        {
            await action(step.Value!).ConfigureAwait(false);
            return step;
        }
        catch (Exception ex)
        {
            return step.Tap(_ => throw ex);
        }
    }

    /// <summary>EN: Documentation for public API. JA: TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    public static async Task<ResultStep<TState, TNext>> Map<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, TNext> selector)
    {
        var step = await task.ConfigureAwait(false);
        return step.Map(selector);
    }

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, bool> predicate)
    {
        var step = await task.ConfigureAwait(false);
        return step.Where(predicate);
    }

    /// <summary>EN: Documentation for public API. JA: TValue&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']/summary" />
    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<bool>> predicate)
    {
        var step = await task.ConfigureAwait(false);
        return await step.Where(predicate).ConfigureAwait(false);
    }

    /// <summary>EN: Documentation for public API. JA: TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    public static Task<ResultStep<TState, TNext>> Select<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, TNext> selector)
        => task.Map(selector);

    /// <summary>EN: Documentation for public API. JA: TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    public static async Task<ResultStep<TState, TNext>> Bind<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<ResultStep<TState, TNext>>> binder)
    {
        var step = await task.ConfigureAwait(false);
        return await step.BindAsync(binder).ConfigureAwait(false);
    }

    /// <summary>EN: Documentation for public API. JA: TNext&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']/summary" />
    public static async Task<ResultStep<TState, TNext>> Bind<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, ResultStep<TState, TNext>> binder)
    {
        var step = await task.ConfigureAwait(false);
        return step.Bind(binder);
    }

    /// <summary>EN: Documentation for public API. JA: TResult&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']/summary" />
    public static async Task<ResultStep<TState, TResult>> SelectMany<TState, TValue, TNext, TResult>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<ResultStep<TState, TNext>>> binder,
        Func<TValue, TNext, TResult> projector)
        => await task
            .Bind(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

    /// <summary>EN: Documentation for public API. JA: TResult&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']/summary" />
    public static async Task<ResultStep<TState, TResult>> SelectMany<TState, TValue, TNext, TResult>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, ResultStep<TState, TNext>> binder,
        Func<TValue, TNext, TResult> projector)
        => await task
            .Bind(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

}
