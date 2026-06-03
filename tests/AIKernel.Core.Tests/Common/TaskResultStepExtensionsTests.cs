namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class TaskResultStepExtensionsTests
{
    [Fact]
    public async Task Bind_ComposesTaskResultStepWithTaskResultStep()
    {
        var step = await SuccessAsync("capability", 2)
            .Bind(value => SuccessAsync("capability:prompt", value + 3));

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal(5, step.Value);
    }

    [Fact]
    public async Task Bind_ComposesTaskResultStepWithSynchronousResultStep()
    {
        var step = await SuccessAsync("capability", 2)
            .Bind(value => ResultStep<string, int>
                .Success("capability:prompt", value + 3));

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal(5, step.Value);
    }

    [Fact]
    public async Task Bind_ShortCircuitsFailureWithoutRunningBinder()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var step = await FailAsync<int>("capability", failure)
            .Bind(value =>
            {
                called = true;
                return SuccessAsync("capability:prompt", value + 3);
            });

        Assert.True(step.IsFailure);
        Assert.False(called);
        Assert.Equal("capability", step.State);
        Assert.Same(failure, step.Error);
    }

    [Fact]
    public async Task Bind_CatchesAsyncBinderExceptionWithCurrentState()
    {
        var step = await SuccessAsync("provider", 2)
            .Bind<string, int, int>(_ => ThrowsAsync());

        Assert.True(step.IsFailure);
        Assert.Equal("provider", step.State);
        Assert.Equal("task-step-binder-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
    }

    [Fact]
    public async Task Bind_CatchesAsyncBinderExceptionAndMarksReplayLogFailure()
    {
        var delta = new SemanticDelta(
            "kernel.provider.generate",
            OriginStep.Provider,
            SemanticSlot.T);

        var step = await Task.FromResult(ResultStep<string, int>
                .Success("provider", 2)
                .WithSemanticDelta(delta))
            .Bind<string, int, int>(_ => ThrowsAsync());

        var entry = Assert.Single(step.ReplayLog);
        Assert.True(step.IsFailure);
        Assert.Equal(step.StepId, entry.StepId);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public async Task Select_MapsValueAndPreservesState()
    {
        var step = await SuccessAsync("prompt", 2)
            .Select(value => value + 3);

        Assert.True(step.IsSuccess);
        Assert.Equal("prompt", step.State);
        Assert.Equal(5, step.Value);
    }

    [Fact]
    public async Task Tap_RunsAsyncActionForSuccessAndPreservesValue()
    {
        var observed = 0;

        var step = await SuccessAsync("provider", 4)
            .Tap(value =>
            {
                observed = value;
                return Task.CompletedTask;
            });

        Assert.True(step.IsSuccess);
        Assert.Equal("provider", step.State);
        Assert.Equal(4, step.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public async Task Tap_CatchesAsyncActionExceptionAndMarksReplayLogFailure()
    {
        var delta = new SemanticDelta(
            "kernel.provider.generate",
            OriginStep.Provider,
            SemanticSlot.T);

        var step = await Task.FromResult(ResultStep<string, int>
                .Success("provider", 4)
                .WithSemanticDelta(delta))
            .Tap(_ => throw new InvalidOperationException("async-tap-boom"));

        var entry = Assert.Single(step.ReplayLog);
        Assert.True(step.IsFailure);
        Assert.Equal("async-tap-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
        Assert.Equal(step.StepId, entry.StepId);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public async Task LinqQuery_ComposesAsyncAndSyncSteps()
    {
        var step = await (
            from capability in SuccessAsync("capability", 2)
            from prompt in ResultStep<string, int>.Success(
                "capability:prompt",
                capability + 3)
            from output in SuccessAsync("capability:prompt:output", prompt + 4)
            select output);

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt:output", step.State);
        Assert.Equal(9, step.Value);
    }

    [Fact]
    public async Task LinqQuery_ReturnsBinderFailureWithLatestState()
    {
        var failure = new ErrorContext("missing", "MISSING", false);

        var step = await (
            from capability in SuccessAsync("capability", 2)
            from prompt in ResultStep<string, int>.Fail(
                "capability:prompt",
                failure)
            from output in SuccessAsync("capability:prompt:output", prompt + 4)
            select output);

        Assert.True(step.IsFailure);
        Assert.Equal("capability:prompt", step.State);
        Assert.Same(failure, step.Error);
    }

    private static Task<ResultStep<string, int>> SuccessAsync(
        string state,
        int value)
    {
        return Task.FromResult(ResultStep<string, int>.Success(state, value));
    }

    private static Task<ResultStep<string, T>> FailAsync<T>(
        string state,
        ErrorContext error)
    {
        return Task.FromResult(ResultStep<string, T>.Fail(state, error));
    }

    private static Task<ResultStep<string, int>> ThrowsAsync()
    {
        throw new InvalidOperationException("task-step-binder-boom");
    }
}
