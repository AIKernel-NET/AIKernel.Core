namespace AIKernel.Common.Results;

public static class EitherWhereExtensions
{
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
