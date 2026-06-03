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
}
