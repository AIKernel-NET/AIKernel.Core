namespace AIKernel.Common.Results;

public static class TaskResultWhereExtensions
{
    public static async Task<Result<T>> Where<T>(
        this Task<Result<T>> task,
        Func<T, bool> predicate)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            if (result.IsFailure)
                return result;

            return predicate(result.Value!)
                ? result
                : Result<T>.Fail(PredicateFailedError());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ErrorContext.FromException(ex));
        }
    }

    private static ErrorContext PredicateFailedError()
    {
        return new ErrorContext("Predicate failed", "PREDICATE_FAILED", false);
    }
}
