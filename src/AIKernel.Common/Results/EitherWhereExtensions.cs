namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: EitherWhereExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.EitherWhereExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.EitherWhereExtensions']/summary" />
public static class EitherWhereExtensions
{
    /// <summary>EN: Documentation for public API. JA: R&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.EitherWhereExtensions.R&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.EitherWhereExtensions.R&gt;']/summary" />
    public static Either<L, R> Where<L, R>(
        this Either<L, R> either,
        Func<R, bool> predicate,
        Func<L> leftFactory)
    {
        if (either.IsLeft)
            return either;

        return predicate(either.Right!)
            ? either
            : Either<L, R>.FromLeft(leftFactory());
    }
}
