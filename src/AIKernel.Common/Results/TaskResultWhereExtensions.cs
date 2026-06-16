namespace AIKernel.Common.Results;

/// <summary>[EN] Documents this public package API member. [JA] TaskResultWhereExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultWhereExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.TaskResultWhereExtensions']/summary" />
public static class TaskResultWhereExtensions
{
    /// <summary>[EN] Documents this public package API member. [JA] Where&lt;T&gt; を取得します。</summary>
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

    /// <summary>[EN] Documents this public package API member. [JA] Where&lt;T&gt; を取得します。</summary>
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
