namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class ResultTests
{
    [Fact]
    public void Map_TransformsSuccess()
    {
        var result = Result<int>.Success(2).Map(x => x + 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Bind_PropagatesFailureWithoutRunningBinder()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = Result<int>
            .Fail(failure)
            .Bind(_ =>
            {
                called = true;
                return Result<string>.Success("unexpected");
            });

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public void LinqQuery_ComposesSuccessfulResults()
    {
        var result =
            from left in Result<int>.Success(2)
            from right in Result<int>.Success(5)
            select left * right;

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void LinqQuery_ReturnsBinderFailure()
    {
        var failure = new ErrorContext("missing", "MISSING", false);

        var result =
            from left in Result<int>.Success(2)
            from right in Result<int>.Fail(failure)
            select left + right;

        Assert.True(result.IsFailure);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public void LinqQuery_CatchesProjectorException()
    {
        var result =
            from value in Result<int>.Success(2)
            from divisor in Result<int>.Success(0)
            select value / divisor;

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
    }

    [Fact]
    public void Where_ReturnsFailure_WhenPredicateFails()
    {
        var result =
            from value in Result<int>.Success(1)
            where value > 1
            select value;

        Assert.True(result.IsFailure);
        Assert.Equal("Predicate failed", result.Error!.Message);
    }
}
