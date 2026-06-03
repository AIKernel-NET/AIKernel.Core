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
    public async Task Bind_ComposesSynchronousResultWithTaskResult()
    {
        var result = await Result<int>
            .Success(3)
            .Bind(value => SuccessAsync(value + 4));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Bind_ComposesTaskResultWithTaskResult()
    {
        var result = await SuccessAsync(3)
            .Bind(value => SuccessAsync(value + 4));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Bind_ComposesTaskResultWithSynchronousResult()
    {
        var result = await SuccessAsync(3)
            .Bind(value => Result<int>.Success(value + 4));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Map_MapsTaskResultSuccess()
    {
        var result = await SuccessAsync(3)
            .Map(value => value + 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Map_CatchesSelectorException()
    {
        var result = await SuccessAsync(3).Map<int, int>(
            _ => throw new InvalidOperationException("map-boom"));

        Assert.True(result.IsFailure);
        Assert.Equal("map-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task Map_ShortCircuitsFailureWithoutRunningSelector()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = await FailAsync<int>(failure)
            .Map(value =>
            {
                called = true;
                return value + 1;
            });

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
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
    public async Task Bind_ShortCircuitsTaskFailureWithoutRunningBinder()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = await FailAsync<int>(failure)
            .Bind(_ =>
            {
                called = true;
                return SuccessAsync(4);
            });

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task Bind_CatchesAsyncBinderException()
    {
        var result = await SuccessAsync(3)
            .Bind<int, int>(_ => ThrowsAsync());

        Assert.True(result.IsFailure);
        Assert.Equal("async-binder-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task Bind_CatchesSynchronousBinderException()
    {
        var result = await SuccessAsync(3)
            .Bind<int, int>(_ => Throws());

        Assert.True(result.IsFailure);
        Assert.Equal("sync-binder-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
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
    public async Task Tap_RunsSynchronousActionForSuccessAndPreservesValue()
    {
        var observed = 0;

        var result = await SuccessAsync(4)
            .Tap(value => observed = value);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_RunsAsyncActionForSuccessAndPreservesValue()
    {
        var observed = 0;

        var result = await SuccessAsync(4)
            .Tap(value =>
            {
                observed = value;
                return Task.CompletedTask;
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_ShortCircuitsFailureWithoutRunningAction()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = await FailAsync<int>(failure)
            .Tap(_ => called = true);

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task Tap_CatchesActionException()
    {
        var result = await SuccessAsync(4)
            .Tap(_ => throw new InvalidOperationException("task-tap-boom"));

        Assert.True(result.IsFailure);
        Assert.Equal("task-tap-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task Tap_CatchesAsyncActionException()
    {
        var result = await SuccessAsync(4)
            .Tap(_ => Task.FromException(new InvalidOperationException("task-async-tap-boom")));

        Assert.True(result.IsFailure);
        Assert.Equal("task-async-tap-boom", result.Error!.Message);
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
