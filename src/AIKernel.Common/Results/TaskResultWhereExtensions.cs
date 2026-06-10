namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultWhereExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultWhereExtensions']/summary" />
public static class TaskResultWhereExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultWhereExtensions.Where&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultWhereExtensions.Where&lt;T&gt;']/summary" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultWhereExtensions.Where&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.TaskResultWhereExtensions.Where&lt;T&gt;']/summary" />
    public static async Task<Result<T>> Where<T>(
        this Task<Result<T>> task,
        Func<T, Task<bool>> predicate)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            if (result.IsFailure)
                return result;

            return await predicate(result.Value!).ConfigureAwait(false)
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
