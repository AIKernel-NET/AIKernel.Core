namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.EitherWhereExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.EitherWhereExtensions']/summary" />
public static class EitherWhereExtensions
{
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
