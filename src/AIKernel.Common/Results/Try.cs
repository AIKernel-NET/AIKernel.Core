namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Try']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Try']" />
public static class Try
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.Run&lt;T&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.Run&lt;T&gt;']" />
    public static Result<T> Run<T>(Func<T> func)
    {
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.RunAsync&lt;T&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.RunAsync&lt;T&gt;']" />
    public static async Task<Result<T>> RunAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return Result<T>.Success(await func().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ErrorContext.FromException(ex));
        }
    }

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.U&gt;']" />
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> mapper)
        => result.Map(mapper);

    /// <summary>Executes the Bind&lt;T, U&gt; operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで Bind&lt;T, U&gt; 操作を実行します。</summary>
    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> binder)
        => result.Bind(binder);
}
