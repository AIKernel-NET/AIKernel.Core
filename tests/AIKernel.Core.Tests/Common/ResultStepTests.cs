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
