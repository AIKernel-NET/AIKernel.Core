namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: ResultWhereExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultWhereExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultWhereExtensions']/summary" />
public static class ResultWhereExtensions
{
    /// <summary>EN: Documentation for public API. JA: Where&lt;T&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultWhereExtensions.Where&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ResultWhereExtensions.Where&lt;T&gt;']/summary" />
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
