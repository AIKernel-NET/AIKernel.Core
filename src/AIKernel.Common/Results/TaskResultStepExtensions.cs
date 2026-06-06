namespace AIKernel.Common.Results;

public static class TaskResultStepExtensions
{
    public static async Task<ResultStep<TState, TValue>> Tap<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Action<TValue> action)
    {
        var step = await task.ConfigureAwait(false);
        return step.Tap(action);
    }

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

    public static async Task<ResultStep<TState, TNext>> Map<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, TNext> selector)
    {
        var step = await task.ConfigureAwait(false);
        return step.Map(selector);
    }

    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, bool> predicate)
    {
        var step = await task.ConfigureAwait(false);
        return step.Where(predicate);
    }

    public static async Task<ResultStep<TState, TValue>> Where<TState, TValue>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<bool>> predicate)
    {
        var step = await task.ConfigureAwait(false);
        return await step.Where(predicate).ConfigureAwait(false);
    }

    public static Task<ResultStep<TState, TNext>> Select<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, TNext> selector)
        => task.Map(selector);

    public static async Task<ResultStep<TState, TNext>> Bind<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<ResultStep<TState, TNext>>> binder)
    {
        var step = await task.ConfigureAwait(false);
        return await step.BindAsync(binder).ConfigureAwait(false);
    }

    public static async Task<ResultStep<TState, TNext>> Bind<TState, TValue, TNext>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, ResultStep<TState, TNext>> binder)
    {
        var step = await task.ConfigureAwait(false);
        return step.Bind(binder);
    }

    public static async Task<ResultStep<TState, TResult>> SelectMany<TState, TValue, TNext, TResult>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, Task<ResultStep<TState, TNext>>> binder,
        Func<TValue, TNext, TResult> projector)
        => await task
            .Bind(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

    public static async Task<ResultStep<TState, TResult>> SelectMany<TState, TValue, TNext, TResult>(
        this Task<ResultStep<TState, TValue>> task,
        Func<TValue, ResultStep<TState, TNext>> binder,
        Func<TValue, TNext, TResult> projector)
        => await task
            .Bind(value => binder(value).Map(next => projector(value, next)))
            .ConfigureAwait(false);

}
