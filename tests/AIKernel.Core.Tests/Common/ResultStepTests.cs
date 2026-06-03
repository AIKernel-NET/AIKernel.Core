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
            .Map(current => current.Value + 3);

        Assert.True(step.IsSuccess);
        Assert.Equal("capability", step.State);
        Assert.Equal(5, step.Value);
    }

    [Fact]
    public void Bind_UpdatesStateAndValue()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .Bind(current => ResultStep<string, string>
                .Success($"{current.State}:prompt", $"value:{current.Value}"));

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
    public void MapState_UpdatesStateOnlyOnSuccess()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .MapState(current => $"{current.State}:prompt");

        Assert.True(step.IsSuccess);
        Assert.Equal("capability:prompt", step.State);
        Assert.Equal(2, step.Value);
    }

    [Fact]
    public void MapState_CatchesStateMapperException()
    {
        var step = ResultStep<string, int>
            .Success("capability", 2)
            .MapState(_ => throw new InvalidOperationException("state-boom"));

        Assert.True(step.IsFailure);
        Assert.Equal("capability", step.State);
        Assert.Equal("state-boom", step.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", step.Error.Code);
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
            .BindAsync(current => Task.FromResult(ResultStep<string, string>
                .Success($"{current.State}:prompt", $"value:{current.Value}")));

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
            .Tap(current => observed = current.Value);

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
    public void LinqQuery_ComposesStepsAndPreservesLatestState()
    {
        var step =
            from capability in ResultStep<string, int>.Success("capability", 2)
            from prompt in ResultStep<string, int>.Success(
                $"{capability.State}:prompt",
                capability.Value + 3)
            select $"{prompt.State}:{prompt.Value}";

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
                $"{capability.State}:prompt",
                failure)
            select $"{prompt.State}:{prompt.Value}";

        Assert.True(step.IsFailure);
        Assert.Equal("capability:prompt", step.State);
        Assert.Same(failure, step.Error);
    }
}
