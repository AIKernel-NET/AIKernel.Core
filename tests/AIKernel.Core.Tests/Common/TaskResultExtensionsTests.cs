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
    public async Task LinqQuery_ComposesSynchronousResultWithTaskResult()
    {
        var result = await (
            from left in Result<int>.Success(3)
            from right in SuccessAsync(4)
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
    public async Task LinqQuery_ShortCircuitsSynchronousInitialFailure()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = await (
            from left in Result<int>.Fail(failure)
            from right in TrackAsync(4, () => called = true)
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

    [Fact]
    public async Task Select_CatchesSelectorException()
    {
        var result = await SuccessAsync(3).Select<int, int>(
            _ => throw new InvalidOperationException("selector-boom"));

        Assert.True(result.IsFailure);
        Assert.Equal("selector-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task LinqQuery_CatchesAsyncBinderException()
    {
        var result = await (
            from left in SuccessAsync(3)
            from right in ThrowsAsync()
            select left + right);

        Assert.True(result.IsFailure);
        Assert.Equal("async-binder-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task LinqQuery_CatchesSynchronousBinderException()
    {
        var result = await (
            from left in SuccessAsync(3)
            from right in Throws()
            select left + right);

        Assert.True(result.IsFailure);
        Assert.Equal("sync-binder-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task LinqQuery_AppliesWhereToTaskResult()
    {
        var result = await (
            from value in SuccessAsync(3)
            where value > 1
            select value * 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value);
    }

    [Fact]
    public async Task LinqQuery_ReturnsFailure_WhenTaskResultPredicateFails()
    {
        var result = await (
            from value in SuccessAsync(1)
            where value > 1
            select value);

        Assert.True(result.IsFailure);
        Assert.Equal("Predicate failed", result.Error!.Message);
        Assert.Equal("PREDICATE_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task LinqQuery_CatchesTaskResultPredicateException()
    {
        var result = await (
            from value in SuccessAsync(1)
            where ThrowsPredicate(value)
            select value);

        Assert.True(result.IsFailure);
        Assert.Equal("task-predicate-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
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

    private static Task<Result<int>> ThrowsAsync()
    {
        throw new InvalidOperationException("async-binder-boom");
    }

    private static Result<int> Throws()
    {
        throw new InvalidOperationException("sync-binder-boom");
    }

    private static bool ThrowsPredicate(int _)
    {
        throw new InvalidOperationException("task-predicate-boom");
    }
}
