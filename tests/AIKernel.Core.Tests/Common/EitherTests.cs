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

    [Fact]
    public void Map_TransformsRight()
    {
        var either = Either<string, int>
            .FromRight(7)
            .Map(value => value * 2);

        Assert.True(either.IsRight);
        Assert.Equal(14, either.Right);
    }

    [Fact]
    public void Bind_ComposesRight()
    {
        var either = Either<string, int>
            .FromRight(7)
            .Bind(value => Either<string, string>.FromRight($"value:{value}"));

        Assert.True(either.IsRight);
        Assert.Equal("value:7", either.Right);
    }

    [Fact]
    public void Bind_ShortCircuitsLeft()
    {
        var called = false;

        var either = Either<string, int>
            .FromLeft("blocked")
            .Bind(_ =>
            {
                called = true;
                return Either<string, string>.FromRight("unexpected");
            });

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    [Fact]
    public void Tap_RunsActionForRightAndPreservesValue()
    {
        var observed = 0;

        var either = Either<string, int>
            .FromRight(4)
            .Tap(value => observed = value);

        Assert.True(either.IsRight);
        Assert.Equal(4, either.Right);
        Assert.Equal(4, observed);
    }

    [Fact]
    public void Tap_ShortCircuitsLeftWithoutRunningAction()
    {
        var called = false;

        var either = Either<string, int>
            .FromLeft("blocked")
            .Tap(_ => called = true);

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    private static Either<string, int> Track(
        int value,
        Action onCall)
    {
        onCall();
        return Either<string, int>.FromRight(value);
    }
}
