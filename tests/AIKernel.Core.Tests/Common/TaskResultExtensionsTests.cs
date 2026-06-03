namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class TaskResultExtensionsTests
{
    [Fact]
    public async Task LinqQuery_ComposesSuccessfulTaskResults()
    {
        var result = await (
            from left in SuccessAsync(3)
            from right in SuccessAsync(4)
            select left + right);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task LinqQuery_ShortCircuitsInitialFailure()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = await (
            from left in FailAsync<int>(failure)
            from right in TrackAsync(4, () => called = true)
            select left + right);

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task LinqQuery_ReturnsBinderFailure()
    {
        var failure = new ErrorContext("missing", "MISSING", false);

        var result = await (
            from left in SuccessAsync(3)
            from right in FailAsync<int>(failure)
            select left + right);

        Assert.True(result.IsFailure);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task LinqQuery_ComposesTaskResultWithSynchronousResult()
    {
        var result = await (
            from left in SuccessAsync(3)
            from right in Result<int>.Success(4)
            select left + right);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task LinqQuery_ShortCircuitsBeforeSynchronousBinder()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = await (
            from left in FailAsync<int>(failure)
            from right in Track(4, () => called = true)
            select left + right);

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task LinqQuery_CatchesProjectorException()
    {
        var result = await (
            from value in SuccessAsync(3)
            from divisor in SuccessAsync(0)
            select value / divisor);

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
    }

    private static Task<Result<int>> SuccessAsync(int value)
    {
        return Task.FromResult(Result<int>.Success(value));
    }

    private static Task<Result<T>> FailAsync<T>(ErrorContext error)
    {
        return Task.FromResult(Result<T>.Fail(error));
    }

    private static Task<Result<int>> TrackAsync(
        int value,
        Action onCall)
    {
        onCall();
        return SuccessAsync(value);
    }

    private static Result<int> Track(
        int value,
        Action onCall)
    {
        onCall();
        return Result<int>.Success(value);
    }
}
