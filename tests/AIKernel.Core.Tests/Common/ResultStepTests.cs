namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class ResultStepTests
{
    [Fact]
    public void Map_TransformsSuccessAndPreservesState()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .Map(value => value + 3);

        Assert.True(step.IsSuccess);
        Assert.Equal("capability", step.State);
        Assert.Equal(5, step.Value);
    }

    [Fact]
    public void Bind_UpdatesStateAndValue()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .Bind(value => ResultStep<string, string>
                .Success("capability:prompt", $"value:{value}"));

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal("value:2", step.Value);
    }

    [Fact]
    public void FromResult_LiftsSuccessWithState()
    {
        var step = ResultStep<string, int>.FromResult(
            "capability",
            Result<int>.Success(4));

        Assert.True(step.IsSuccess);
        Assert.Equal("capability", step.State);
        Assert.Equal(4, step.Value);
    }

    [Fact]
    public void FromResult_LiftsFailureWithState()
    {
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var step = ResultStep<string, int>.FromResult(
            "capability",
            Result<int>.Fail(failure));

        Assert.True(step.IsFailure);
        Assert.Equal("capability", step.State);
        Assert.Same(failure, step.Error);
    }

    [Fact]
    public void StepId_IsDeterministic_ForSameSemanticDelta()
    {
        var delta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);

        var first = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(delta);
        var second = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(delta);

        Assert.Equal(first.StepId, second.StepId);
        Assert.StartsWith("step:sha256:", first.StepId, StringComparison.Ordinal);
        Assert.Equal(delta, first.SemanticDelta);
    }

    [Fact]
    public void StepId_Changes_WhenParentStepIdChanges()
    {
        var delta = new SemanticDelta(
            "kernel.prompt.generate",
            OriginStep.Prompt,
            SemanticSlot.T);

        var first = ResultStep<string, int>
            .Success("prompt", 2)
            .WithSemanticDelta(delta, parentStepId: "step:sha256:parent-a");
        var second = ResultStep<string, int>
            .Success("prompt", 2)
            .WithSemanticDelta(delta, parentStepId: "step:sha256:parent-b");

        Assert.NotEqual(first.StepId, second.StepId);
    }

    [Fact]
    public void Bind_ReparentsNextStepToCurrentStepId()
    {
        var firstDelta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);
        var secondDelta = new SemanticDelta(
            "kernel.prompt.generate",
            OriginStep.Prompt,
            SemanticSlot.T);

        var first = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(firstDelta);
        var bound = first.Bind(value => ResultStep<string, string>
            .Success("prompt", $"value:{value}")
            .WithSemanticDelta(secondDelta));
        var expected = ResultStep<string, string>
            .Success("prompt", "value:2")
            .WithSemanticDelta(secondDelta, first.StepId);

        Assert.Equal(expected.StepId, bound.StepId);
        Assert.Equal(secondDelta, bound.SemanticDelta);
    }

    [Fact]
    public void Bind_AppendsDeterministicReplayLog()
    {
        var firstDelta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);
        var secondDelta = new SemanticDelta(
            "kernel.prompt.generate",
            OriginStep.Prompt,
            SemanticSlot.T);

        var first = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(firstDelta);
        var bound = first.Bind(value => ResultStep<string, string>
            .Success("prompt", $"value:{value}")
            .WithSemanticDelta(secondDelta));

        Assert.Equal(2, bound.ReplayLog.Count);
        Assert.Equal(first.StepId, bound.ReplayLog[0].StepId);
        Assert.Equal(firstDelta, bound.ReplayLog[0].SemanticDelta);
        Assert.Equal(bound.StepId, bound.ReplayLog[1].StepId);
        Assert.Equal(first.StepId, bound.ReplayLog[1].ParentStepId);
        Assert.Equal(secondDelta, bound.ReplayLog[1].SemanticDelta);
        Assert.StartsWith("replay:sha256:", bound.ReplayLogHash, StringComparison.Ordinal);

        var repeated = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(firstDelta)
            .Bind(value => ResultStep<string, string>
                .Success("prompt", $"value:{value}")
                .WithSemanticDelta(secondDelta));

        Assert.Equal(bound.ReplayLogHash, repeated.ReplayLogHash);
    }

    [Fact]
    public void LinqQuery_AppendsReplayLogWithDeterministicParentChain()
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

        var step =
            from capability in ResultStep<string, int>
                .Success("capability", 2)
                .WithSemanticDelta(capabilityDelta)
            from prompt in ResultStep<string, int>
                .Success("prompt", capability + 3)
                .WithSemanticDelta(promptDelta)
            from output in ResultStep<string, int>
                .Success("output", prompt + 4)
                .WithSemanticDelta(providerDelta)
            select output;

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
    public void Map_PreservesReplayLogWithoutAddingProjectionNode()
    {
        var delta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);

        var step = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(delta)
            .Map(value => value + 1);

        var entry = Assert.Single(step.ReplayLog);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public void MapState_UpdatesStateOnlyOnSuccess()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .MapState((state, _) => $"{state}:prompt");

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal(2, step.Value);
    }

    [Fact]
    public void MapState_CatchesStateMapperException()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .MapState((_, _) => throw new InvalidOperationException("state-boom"));

        Assert.True(step.IsFailure);
        Assert.Equal("capability", step.State);
        Assert.Equal("state-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
    }

    [Fact]
    public void MapState_ExceptionBeforeSemanticDeltaCreatesFailureReplayLogEntry()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .MapState((_, _) => throw new InvalidOperationException("state-boom"));

        var entry = Assert.Single(step.ReplayLog);
        Assert.True(step.IsFailure);
        Assert.Equal(step.StepId, entry.StepId);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal(SemanticDelta.Empty, entry.SemanticDelta);
    }

    [Fact]
    public void MapState_ExceptionMarksCurrentReplayLogEntryAsFailure()
    {
        var delta = new SemanticDelta(
            "kernel.capability.resolve",
            OriginStep.Capability,
            SemanticSlot.T);

        var step = ResultStep<string, int>
            .Success("capability", 2)
            .WithSemanticDelta(delta)
            .MapState((_, _) => throw new InvalidOperationException("state-boom"));

        var entry = Assert.Single(step.ReplayLog);
        Assert.True(step.IsFailure);
        Assert.Equal(step.StepId, entry.StepId);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public void Bind_PropagatesFailureWithStateWithoutRunningBinder()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var step = ResultStep<string, int>
            .Fail("capability", failure)
            .Bind(_ =>
            {
                called = true;
                return ResultStep<string, string>.Success("unexpected", "unexpected");
            });

        Assert.True(step.IsFailure);
        Assert.False(called);
        Assert.Equal("capability", step.State);
        Assert.Same(failure, step.Error);
    }

    [Fact]
    public void Bind_CatchesBinderExceptionAsUnhandledExceptionWithState()
    {
        var step = ResultStep<string, int>
            .Success("provider", 2)
            .Bind<string>(_ => throw new InvalidOperationException("step-boom"));

        Assert.True(step.IsFailure);
        Assert.Equal("provider", step.State);
        Assert.Equal("step-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
    }

    [Fact]
    public async Task BindAsync_ComposesSuccessAndPreservesUpdatedState()
    {
        var step = await ResultStep<string, int>
            .Success("capability", 2)
            .BindAsync(value => Task.FromResult(ResultStep<string, string>
                .Success("capability:prompt", $"value:{value}")));

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal("value:2", step.Value);
    }

    [Fact]
    public async Task BindAsync_ShortCircuitsFailureWithoutRunningBinder()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var step = await ResultStep<string, int>
            .Fail("capability", failure)
            .BindAsync(_ =>
            {
                called = true;
                return Task.FromResult(ResultStep<string, string>
                    .Success("unexpected", "unexpected"));
            });

        Assert.True(step.IsFailure);
        Assert.False(called);
        Assert.Equal("capability", step.State);
        Assert.Same(failure, step.Error);
    }

    [Fact]
    public async Task BindAsync_CatchesBinderExceptionAsUnhandledExceptionWithState()
    {
        var step = await ResultStep<string, int>
            .Success("provider", 2)
            .BindAsync<string>(_ => throw new InvalidOperationException("async-step-boom"));

        Assert.True(step.IsFailure);
        Assert.Equal("provider", step.State);
        Assert.Equal("async-step-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
    }

    [Fact]
    public void Tap_RunsActionForSuccessAndPreservesValue()
    {
        var observed = 0;

        var step = ResultStep<string, int>
            .Success("provider", 4)
            .Tap(value => observed = value);

        Assert.True(step.IsSuccess);
        Assert.Equal(4, step.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public void Tap_CatchesActionException()
    {
        var step = ResultStep<string, int>
            .Success("provider", 4)
            .Tap(_ => throw new InvalidOperationException("tap-boom"));

        Assert.True(step.IsFailure);
        Assert.Equal("provider", step.State);
        Assert.Equal("tap-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
    }

    [Fact]
    public void Tap_ExceptionMarksCurrentReplayLogEntryAsFailure()
    {
        var delta = new SemanticDelta(
            "kernel.provider.generate",
            OriginStep.Provider,
            SemanticSlot.T);

        var step = ResultStep<string, int>
            .Success("provider", 4)
            .WithSemanticDelta(delta)
            .Tap(_ => throw new InvalidOperationException("tap-boom"));

        var entry = Assert.Single(step.ReplayLog);
        Assert.True(step.IsFailure);
        Assert.Equal(step.StepId, entry.StepId);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal(delta, entry.SemanticDelta);
    }

    [Fact]
    public void LinqQuery_ComposesStepsAndPreservesLatestState()
    {
        var step =
            from capability in ResultStep<string, int>.Success("capability", 2)
            from prompt in ResultStep<string, int>.Success(
                "capability:prompt",
                capability + 3)
            select $"capability:prompt:{prompt}";

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal("capability:prompt:5", step.Value);
    }

    [Fact]
    public void LinqQuery_ReturnsBinderFailureWithLatestState()
    {
        var failure = new ErrorContext("missing", "MISSING", false);

        var step =
            from capability in ResultStep<string, int>.Success("capability", 2)
            from prompt in ResultStep<string, int>.Fail(
                "capability:prompt",
                failure)
            select $"capability:prompt:{prompt}";

        Assert.True(step.IsFailure);
        Assert.Equal("capability:prompt", step.State);
        Assert.Same(failure, step.Error);
    }
}
