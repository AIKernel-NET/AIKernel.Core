namespace AIKernel.Common.Results;

public static class Try
{
    public static Result<T> Run<T>(Func<T> func)
    {
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex.Message);
        }
    }

    public static async Task<Result<T>> RunAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return Result<T>.Success(await func());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex.Message);
        }
    }

    // -------------------------
    // Functional Extensions
    // -------------------------

    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> mapper)
        => result.Map(mapper);

    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> binder)
        => result.Bind(binder);
}
