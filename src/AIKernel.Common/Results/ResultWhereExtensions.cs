namespace AIKernel.Common.Results;

public static class ResultWhereExtensions
{
    public static Result<T> Where<T>(
        this Result<T> result,
        Func<T, bool> predicate)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value!)
            ? result
            : Result<T>.Fail("Predicate failed");
    }
}
