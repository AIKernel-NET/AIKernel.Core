namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class TaskOptionExtensionsTests
{
    [Fact]
    public async Task LinqQuery_ComposesSomeValues()
    {
        var option = await (
            from left in SomeAsync(2)
            from right in SomeAsync(4)
            select left * right);

        Assert.True(option.HasValue);
        Assert.Equal(8, option.Value);
    }

    [Fact]
    public async Task LinqQuery_ShortCircuitsNone()
    {
        var called = false;

        var option = await (
            from left in NoneAsync<int>()
            from right in TrackAsync(4, () => called = true)
            select left + right);

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    [Fact]
    public async Task LinqQuery_ComposesTaskOptionWithSynchronousOption()
    {
        var option = await (
            from left in SomeAsync(2)
            from right in Option<int>.Some(4)
            select left * right);

        Assert.True(option.HasValue);
        Assert.Equal(8, option.Value);
    }

    [Fact]
    public async Task LinqQuery_ComposesSynchronousOptionWithTaskOption()
    {
        var option = await (
            from left in Option<int>.Some(2)
            from right in SomeAsync(4)
            select left * right);

        Assert.True(option.HasValue);
        Assert.Equal(8, option.Value);
    }

    [Fact]
    public async Task AsTask_LiftsSynchronousOptionIntoTaskOption()
    {
        var option = await Option<int>
            .Some(3)
            .AsTask();

        Assert.True(option.HasValue);
        Assert.Equal(3, option.Value);
    }

    [Fact]
    public async Task LinqQuery_ComposesOptionAsTaskWithTaskOption()
    {
        var option = await (
            from left in Option<int>.Some(2).AsTask()
            from right in SomeAsync(4)
            select left * right);

        Assert.True(option.HasValue);
        Assert.Equal(8, option.Value);
    }

    [Fact]
    public async Task Bind_ComposesSynchronousOptionWithTaskOption()
    {
        var option = await Option<int>
            .Some(3)
            .Bind(value => SomeAsync(value + 4));

        Assert.True(option.HasValue);
        Assert.Equal(7, option.Value);
    }

    [Fact]
    public async Task Bind_ComposesTaskOptionWithTaskOption()
    {
        var option = await SomeAsync(3)
            .Bind(value => SomeAsync(value + 4));

        Assert.True(option.HasValue);
        Assert.Equal(7, option.Value);
    }

    [Fact]
    public async Task Bind_ComposesTaskOptionWithSynchronousOption()
    {
        var option = await SomeAsync(3)
            .Bind(value => Option<int>.Some(value + 4));

        Assert.True(option.HasValue);
        Assert.Equal(7, option.Value);
    }

    [Fact]
    public async Task Map_MapsTaskOptionSome()
    {
        var option = await SomeAsync(3)
            .Map(value => value + 4);

        Assert.True(option.HasValue);
        Assert.Equal(7, option.Value);
    }

    [Fact]
    public async Task Map_PropagatesSelectorException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SomeAsync(3).Map<int, int>(
                _ => throw new InvalidOperationException("option-map-boom")));

        Assert.Equal("option-map-boom", exception.Message);
    }

    [Fact]
    public async Task Map_ShortCircuitsNoneWithoutRunningSelector()
    {
        var called = false;

        var option = await NoneAsync<int>()
            .Map(value =>
            {
                called = true;
                return value + 1;
            });

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    [Fact]
    public async Task LinqQuery_ShortCircuitsSynchronousNoneBeforeTaskOption()
    {
        var called = false;

        var option = await (
            from left in Option<int>.None()
            from right in TrackAsync(4, () => called = true)
            select left + right);

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    [Fact]
    public async Task Bind_ShortCircuitsTaskNoneWithoutRunningBinder()
    {
        var called = false;

        var option = await NoneAsync<int>()
            .Bind(value =>
            {
                called = true;
                return SomeAsync(value + 4);
            });

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    [Fact]
    public async Task Bind_PropagatesAsyncBinderException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SomeAsync(3)
                .Bind<int, int>(_ => ThrowsAsync()));

        Assert.Equal("option-binder-boom", exception.Message);
    }

    [Fact]
    public async Task Bind_PropagatesSynchronousBinderException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SomeAsync(3)
                .Bind<int, int>(_ => Throws()));

        Assert.Equal("option-sync-binder-boom", exception.Message);
    }

    [Fact]
    public async Task LinqQuery_AppliesWhereToTaskOption()
    {
        var option = await (
            from value in SomeAsync(4)
            where value > 1
            select value * 2);

        Assert.True(option.HasValue);
        Assert.Equal(8, option.Value);
    }

    [Fact]
    public async Task LinqQuery_ReturnsNone_WhenTaskOptionPredicateFails()
    {
        var option = await (
            from value in SomeAsync(1)
            where value > 1
            select value);

        Assert.False(option.HasValue);
    }

    [Fact]
    public async Task Where_AwaitsAsyncPredicate()
    {
        var option = await SomeAsync(4)
            .Where(value => Task.FromResult(value > 1));

        Assert.True(option.HasValue);
        Assert.Equal(4, option.Value);
    }

    [Fact]
    public async Task Where_PropagatesAsyncPredicateException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SomeAsync(4)
                .Where<int>(_ => Task.FromException<bool>(
                    new InvalidOperationException("option-async-predicate-boom"))));

        Assert.Equal("option-async-predicate-boom", exception.Message);
    }

    [Fact]
    public async Task Tap_RunsSynchronousActionForSomeAndPreservesValue()
    {
        var observed = 0;

        var option = await SomeAsync(4)
            .Tap(value => observed = value);

        Assert.True(option.HasValue);
        Assert.Equal(4, option.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_RunsAsyncActionForSomeAndPreservesValue()
    {
        var observed = 0;

        var option = await SomeAsync(4)
            .Tap(value =>
            {
                observed = value;
                return Task.CompletedTask;
            });

        Assert.True(option.HasValue);
        Assert.Equal(4, option.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_ShortCircuitsNoneWithoutRunningAction()
    {
        var called = false;

        var option = await NoneAsync<int>()
            .Tap(_ => called = true);

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    private static Task<Option<int>> SomeAsync(int value)
    {
        return Task.FromResult(Option<int>.Some(value));
    }

    private static Task<Option<T>> NoneAsync<T>()
    {
        return Task.FromResult(Option<T>.None());
    }

    private static Task<Option<int>> TrackAsync(
        int value,
        Action onCall)
    {
        onCall();
        return SomeAsync(value);
    }

    private static Task<Option<int>> ThrowsAsync()
    {
        throw new InvalidOperationException("option-binder-boom");
    }

    private static Option<int> Throws()
    {
        throw new InvalidOperationException("option-sync-binder-boom");
    }
}
