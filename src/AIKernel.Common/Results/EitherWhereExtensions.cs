namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.EitherWhereExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.EitherWhereExtensions']" />
public static class EitherWhereExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.EitherWhereExtensions.R&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.EitherWhereExtensions.R&gt;']" />
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
