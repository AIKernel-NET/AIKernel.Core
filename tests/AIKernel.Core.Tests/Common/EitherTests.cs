namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class EitherTests
{
    [Fact]
    public void LinqQuery_ComposesRightValues()
    {
        var either =
            from left in Either<string, int>.FromRight(2)
            from right in Either<string, int>.FromRight(3)
            select left + right;

        Assert.True(either.IsRight);
        Assert.Equal(5, either.Right);
    }

    [Fact]
    public void LinqQuery_ShortCircuitsLeft()
    {
        var called = false;

        var either =
            from left in Either<string, int>.FromLeft("blocked")
            from right in Track(4, () => called = true)
            select left + right;

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    [Fact]
    public void Where_ReturnsLeft_WhenPredicateFails()
    {
        var either = EitherWhereExtensions.Where(
            Either<string, int>.FromRight(1),
            value => value > 1,
            () => "too-small");

        Assert.True(either.IsLeft);
        Assert.Equal("too-small", either.Left);
    }

    [Fact]
    public void Match_UsesRightBranch_ForRight()
    {
        var either = Either<string, int>.FromRight(7);

        var result = either.Match(
            left => left.Length,
            right => right * 2);

        Assert.Equal(14, result);
    }

    private static Either<string, int> Track(
        int value,
        Action onCall)
    {
        onCall();
        return Either<string, int>.FromRight(value);
    }
}
