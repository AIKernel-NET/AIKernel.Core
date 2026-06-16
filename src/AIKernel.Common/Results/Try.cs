namespace AIKernel.Common.Results;

/// <summary>[EN] Documents this public package API member. [JA] Try を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Try']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Try']/summary" />
public static class Try
{
    /// <summary>[EN] Documents this public package API member. [JA] Run&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.Run&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.Run&lt;T&gt;']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] RunAsync&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.RunAsync&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.RunAsync&lt;T&gt;']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Try.U&gt;']/summary" />
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> mapper)
        => result.Map(mapper);

    /// <summary>
    /// [EN] Binds a successful result to the next result-producing function.
    /// [JA] 成功した result を次の result 生成関数へ bind します。
    /// </summary>
    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> binder)
        => result.Bind(binder);
}
