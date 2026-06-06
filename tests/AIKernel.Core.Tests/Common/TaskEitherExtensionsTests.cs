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

    [Fact]
    public async Task LinqQuery_ComposesTaskEitherWithSynchronousEither()
    {
        var either = await (
            from left in RightAsync(2)
            from right in Either<string, int>.FromRight(4)
            select left * right);

        Assert.True(either.IsRight);
        Assert.Equal(8, either.Right);
    }

    [Fact]
    public async Task LinqQuery_ComposesSynchronousEitherWithTaskEither()
    {
        var either = await (
            from left in Either<string, int>.FromRight(2)
            from right in RightAsync(4)
            select left * right);

        Assert.True(either.IsRight);
        Assert.Equal(8, either.Right);
    }

    [Fact]
    public async Task Bind_ComposesSynchronousEitherWithTaskEither()
    {
        var either = await Either<string, int>
            .FromRight(3)
            .Bind(value => RightAsync(value + 4));

        Assert.True(either.IsRight);
        Assert.Equal(7, either.Right);
    }

    [Fact]
    public async Task Bind_ComposesTaskEitherWithTaskEither()
    {
        var either = await RightAsync(3)
            .Bind(value => RightAsync(value + 4));

        Assert.True(either.IsRight);
        Assert.Equal(7, either.Right);
    }

    [Fact]
    public async Task Bind_ComposesTaskEitherWithSynchronousEither()
    {
        var either = await RightAsync(3)
            .Bind(value => Either<string, int>.FromRight(value + 4));

        Assert.True(either.IsRight);
        Assert.Equal(7, either.Right);
    }

    [Fact]
    public async Task Map_MapsTaskEitherRight()
    {
        var either = await RightAsync(3)
            .Map(value => value + 4);

        Assert.True(either.IsRight);
        Assert.Equal(7, either.Right);
    }

    [Fact]
    public async Task Map_PropagatesSelectorException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await RightAsync(3).Map<string, int, int>(
                _ => throw new InvalidOperationException("either-map-boom")));

        Assert.Equal("either-map-boom", exception.Message);
    }

    [Fact]
    public async Task Map_ShortCircuitsLeftWithoutRunningSelector()
    {
        var called = false;

        var either = await LeftAsync("blocked")
            .Map(value =>
            {
                called = true;
                return value + 1;
            });

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    [Fact]
    public async Task LinqQuery_ShortCircuitsSynchronousLeftBeforeTaskEither()
    {
        var called = false;

        var either = await (
            from left in Either<string, int>.FromLeft("blocked")
            from right in TrackAsync(4, () => called = true)
            select left + right);

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    [Fact]
    public async Task Bind_ShortCircuitsTaskLeftWithoutRunningBinder()
    {
        var called = false;

        var either = await LeftAsync("blocked")
            .Bind(value =>
            {
                called = true;
                return RightAsync(value + 4);
            });

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    [Fact]
    public async Task Bind_PropagatesAsyncBinderException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await RightAsync(3)
                .Bind<string, int, int>(_ => ThrowsAsync()));

        Assert.Equal("either-binder-boom", exception.Message);
    }

    [Fact]
    public async Task Bind_PropagatesSynchronousBinderException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await RightAsync(3)
                .Bind<string, int, int>(_ => Throws()));

        Assert.Equal("either-sync-binder-boom", exception.Message);
    }

    [Fact]
    public async Task LinqQuery_ReturnsSynchronousBinderLeft()
    {
        var either = await (
            from left in RightAsync(2)
            from right in Either<string, int>.FromLeft("missing")
            select left * right);

        Assert.True(either.IsLeft);
        Assert.Equal("missing", either.Left);
    }

    [Fact]
    public async Task Tap_RunsSynchronousActionForRightAndPreservesValue()
    {
        var observed = 0;

        var either = await RightAsync(4)
            .Tap(value => observed = value);

        Assert.True(either.IsRight);
        Assert.Equal(4, either.Right);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_RunsAsyncActionForRightAndPreservesValue()
    {
        var observed = 0;

        var either = await RightAsync(4)
            .Tap(value =>
            {
                observed = value;
                return Task.CompletedTask;
            });

        Assert.True(either.IsRight);
        Assert.Equal(4, either.Right);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_ShortCircuitsLeftWithoutRunningAction()
    {
        var called = false;

        var either = await LeftAsync("blocked")
            .Tap(_ => called = true);

        Assert.True(either.IsLeft);
        Assert.False(called);
        Assert.Equal("blocked", either.Left);
    }

    [Fact]
    public async Task Where_ReturnsRight_WhenPredicatePasses()
    {
        var either = await RightAsync(4)
            .Where(value => value > 1, () => "too-small");

        Assert.True(either.IsRight);
        Assert.Equal(4, either.Right);
    }

    [Fact]
    public async Task Where_ReturnsLeft_WhenPredicateFails()
    {
        var either = await RightAsync(1)
            .Where(value => value > 1, () => "too-small");

        Assert.True(either.IsLeft);
        Assert.Equal("too-small", either.Left);
    }

    [Fact]
    public async Task Where_AwaitsAsyncPredicate()
    {
        var either = await RightAsync(4)
            .Where(value => Task.FromResult(value > 1), () => "too-small");

        Assert.True(either.IsRight);
        Assert.Equal(4, either.Right);
    }

    [Fact]
    public async Task Where_PropagatesAsyncPredicateException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await RightAsync(4)
                .Where<string, int>(
                    _ => Task.FromException<bool>(
                        new InvalidOperationException("either-async-predicate-boom")),
                    () => "blocked"));

        Assert.Equal("either-async-predicate-boom", exception.Message);
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

    private static Task<Either<string, int>> ThrowsAsync()
    {
        throw new InvalidOperationException("either-binder-boom");
    }

    private static Either<string, int> Throws()
    {
        throw new InvalidOperationException("either-sync-binder-boom");
    }
}
