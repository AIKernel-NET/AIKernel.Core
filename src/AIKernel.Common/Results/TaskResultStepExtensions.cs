namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultStepExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultStepExtensions']" />
public static class TaskResultStepExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    public static Task<ResultStep<TState, TValue>> AsTask<TState, TValue>(
        this ResultStep<TState, TValue> step)
        => Task.FromResult(step);

    /// <summary>Executes the Tap&lt;TState, TValue&gt; operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで Tap&lt;TState, TValue&gt; 操作を実行します。</summary>
    public static async Task<ResultStep<TState, TValue>> Tap<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Action<TValue> action)
    {
        var step = await task.ConfigureAwait(false);
        return step.Tap(action);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    public static async Task<ResultStep<TState, TNext>> Map<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, TNext> selector)
    {
        var step = await task.ConfigureAwait(false);
        return step.Map(selector);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, bool> predicate)
    {
        var step = await task.ConfigureAwait(false);
        return step.Where(predicate);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TValue&gt;']" />
    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<bool>> predicate)
    {
        var step = await task.ConfigureAwait(false);
        return await step.Where(predicate).ConfigureAwait(false);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    public static Task<ResultStep<TState, TNext>> Select<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, TNext> selector)
        => task.Map(selector);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    public static async Task<ResultStep<TState, TNext>> Bind<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<ResultStep<TState, TNext>>> binder)
    {
        var step = await task.ConfigureAwait(false);
        return await step.BindAsync(binder).ConfigureAwait(false);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TNext&gt;']" />
    public static async Task<ResultStep<TState, TNext>> Bind<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, ResultStep<TState, TNext>> binder)
    {
        var step = await task.ConfigureAwait(false);
        return step.Bind(binder);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']" />
    public static async Task<ResultStep<TState, TResult>> SelectMany<TState, TValue, TNext, TResult>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<ResultStep<TState, TNext>>> binder,
        Func<TValue, TNext, TResult> projector)
        => await task
            .Bind(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultStepExtensions.TResult&gt;']" />
    public static async Task<ResultStep<TState, TResult>> SelectMany<TState, TValue, TNext, TResult>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, ResultStep<TState, TNext>> binder,
        Func<TValue, TNext, TResult> projector)
        => await task
            .Bind(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

}
