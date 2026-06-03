namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class TaskEitherExtensionsTests
{
    [Fact]
    public async Task LinqQuery_ComposesRightValues()
    {
        var either = await (
            from left in RightAsync(2)
            from right in RightAsync(4)
            select left * right);

        Assert.True(either.IsRight);
        Assert.Equal(8, either.Right);
    }

    [Fact]
    public async Task LinqQuery_ShortCircuitsLeft()
    {
        var called = false;

        var either = await (
            from left in LeftAsync("blocked")
            from right in TrackAsync(4, () => called = true)
            select left + right);

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    private static Task<Either<string, int>> RightAsync(int value)
    {
        return Task.FromResult(Either<string, int>.FromRight(value));
    }

    private static Task<Either<string, int>> LeftAsync(string value)
    {
        return Task.FromResult(Either<string, int>.FromLeft(value));
    }

    private static Task<Either<string, int>> TrackAsync(
        int value,
        Action onCall)
    {
        onCall();
        return RightAsync(value);
    }
}
