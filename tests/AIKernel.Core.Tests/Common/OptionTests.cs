namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class OptionTests
{
    [Fact]
    public void LinqQuery_ComposesSomeValues()
    {
        var option =
            from left in Option<int>.Some(2)
            from right in Option<int>.Some(3)
            select left + right;

        Assert.True(option.HasValue);
        Assert.Equal(5, option.Value);
    }

    [Fact]
    public void LinqQuery_ShortCircuitsNone()
    {
        var called = false;

        var option =
            from left in Option<int>.None()
            from right in Track(4, () => called = true)
            select left + right;

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    [Fact]
    public void Where_ReturnsNone_WhenPredicateFails()
    {
        var option =
            from value in Option<int>.Some(1)
            where value > 1
            select value;

        Assert.False(option.HasValue);
    }

    [Fact]
    public void OrElse_ReturnsFallback_ForNone()
    {
        Assert.Equal(9, Option<int>.None().OrElse(9));
    }

    [Fact]
    public void Bind_ComposesSomeValues()
    {
        var option = Option<int>
            .Some(2)
            .Bind(value => Option<string>.Some($"value:{value}"));

        Assert.True(option.HasValue);
        Assert.Equal("value:2", option.Value);
    }

    [Fact]
    public void Bind_ShortCircuitsNone()
    {
        var called = false;

        var option = Option<int>
            .None()
            .Bind(_ =>
            {
                called = true;
                return Option<string>.Some("unexpected");
            });

        Assert.False(option.HasValue);
        Assert.False(called);
    }

    private static Option<int> Track(
        int value,
        Action onCall)
    {
        onCall();
        return Option<int>.Some(value);
    }
}
