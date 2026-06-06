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
    public async Task Map_MapsValueAndPreservesState()
    {
        var step = await SuccessAsync("prompt", 2)
            .Map(value => value + 3);

        Assert.True(step.IsSuccess);
        Assert.Equal("prompt", step.State);
        Assert.Equal(5, step.Value);
    }

    [Fact]
    public async Task Map_CatchesSelectorExceptionWithCurrentState()
    {
        var step = await SuccessAsync("prompt", 2)
            .Map<string, int, int>(_ => throw new InvalidOperationException("step-map-boom"));

        Assert.True(step.IsFailure);
        Assert.Equal("prompt", step.State);
        Assert.Equal("step-map-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
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
    public async Task LinqQuery_AppendsReplayLogWithDeterministicParentChain()
    {
        var capabilityDelta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);
        var promptDelta = new SemanticDelta(
            "kernel.prompt.generate",
            OriginStep.Prompt,
            SemanticSlot.T);
        var providerDelta = new SemanticDelta(
            "kernel.provider.generate",
            OriginStep.Provider,
            SemanticSlot.T);

        var step = await (
            from capability in Task.FromResult(ResultStep<string, int>
                .Success("capability", 2)
                .WithSemanticDelta(capabilityDelta))
            from prompt in ResultStep<string, int>
                .Success("prompt", capability + 3)
                .WithSemanticDelta(promptDelta)
            from output in Task.FromResult(ResultStep<string, int>
                .Success("output", prompt + 4)
                .WithSemanticDelta(providerDelta))
            select output);

        Assert.True(step.IsSuccess);
        Assert.Equal(3, step.ReplayLog.Count);
        Assert.Equal(capabilityDelta, step.ReplayLog[0].SemanticDelta);
        Assert.Equal(promptDelta, step.ReplayLog[1].SemanticDelta);
        Assert.Equal(providerDelta, step.ReplayLog[2].SemanticDelta);
        Assert.Equal(step.ReplayLog[0].StepId, step.ReplayLog[1].ParentStepId);
        Assert.Equal(step.ReplayLog[1].StepId, step.ReplayLog[2].ParentStepId);
        Assert.Equal(step.StepId, step.ReplayLog[2].StepId);
    }

    [Fact]
    public async Task LinqQuery_CatchesProjectorExceptionAndMarksCurrentReplayLogFailure()
    {
        var delta = new SemanticDelta(
            "kernel.provider.generate",
            OriginStep.Provider,
            SemanticSlot.T);

        var step = await (
            from output in Task.FromResult(ResultStep<string, int>
                .Success("output", 9)
                .WithSemanticDelta(delta))
            select ThrowProjector(output));

        var entry = Assert.Single(step.ReplayLog);
        Assert.True(step.IsFailure);
        Assert.Equal("task-step-projector-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public async Task LinqQuery_WherePassesWithoutAppendingReplayNode()
    {
        var delta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);

        var step = await (
            from value in Task.FromResult(ResultStep<string, int>
                .Success("capability", 2)
                .WithSemanticDelta(delta))
            where value == 2
            select value + 1);

        Assert.True(step.IsSuccess);
        Assert.Equal(3, step.Value);
        var entry = Assert.Single(step.ReplayLog);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public async Task LinqQuery_WhereFailureAppendsRejectReplayNode()
    {
        var delta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);

        var step = await (
            from value in Task.FromResult(ResultStep<string, int>
                .Success("capability", 2)
                .WithSemanticDelta(delta))
            where value > 2
            select value);

        Assert.True(step.IsFailure);
        Assert.Equal("PREDICATE_FAILED", step.Error!.Code);
        Assert.Equal(FailureKind.Reject, step.Error.FailureKind);
        Assert.Equal(2, step.ReplayLog.Count);
        Assert.Equal("result_step.where", step.ReplayLog[1].SemanticDelta.Label);
        Assert.Equal("reject", step.ReplayLog[1].SemanticDelta.Kind);
    }

    [Fact]
    public async Task LinqQuery_WhereExceptionAppendsFailClosedReplayNode()
    {
        var delta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);

        var step = await (
            from value in Task.FromResult(ResultStep<string, int>
                .Success("capability", 2)
                .WithSemanticDelta(delta))
            where Throws(value)
            select value);

        Assert.True(step.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, step.Error.FailureKind);
        Assert.Equal(2, step.ReplayLog.Count);
        Assert.Equal("result_step.where", step.ReplayLog[1].SemanticDelta.Label);
        Assert.Equal("fail_closed", step.ReplayLog[1].SemanticDelta.Kind);
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

    private static int ThrowProjector(int value)
    {
        throw new InvalidOperationException("task-step-projector-boom");
    }

    private static bool Throws(int value)
    {
        throw new InvalidOperationException("task-step-predicate-boom");
    }
}
