namespace AIKernel.Common.Results;

public static class ResultWhereExtensions
{
    public static Result<T> Where<T>(
        this Result<T> result,
        Func<T, bool> predicate)
    {
        if (result.IsFailure)
            return result;

        try
        {
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
