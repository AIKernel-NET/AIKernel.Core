namespace AIKernel.Core.Tests.Common;

using System.Text.Json;
using AIKernel.Common.Results;
using Xunit;

public sealed class DefaultValueSafetyTests
{
    [Theory]
    [InlineData("default", false)]
    [InlineData("new", false)]
    [InlineData("array", false)]
    [InlineData("default", true)]
    [InlineData("new", true)]
    [InlineData("array", true)]
    public async Task UninitializedResultNeverRunsSuccessContinuations(string source, bool referenceType)
    {
        if (referenceType)
            await CheckResult<string>(source);
        else
            await CheckResult<int>(source);
    }

    private static async Task CheckResult<T>(string source)
    {
        Result<T> result = source switch
        {
            "new" => new Result<T>(),
            "array" => (new Result<T>[1])[0],
            _ => default
        };
        AssertFailure(result);
        Assert.False(result.IsSuccessState);
        var calls = 0;
        Assert.Equal("failure", result.Match(_ => "failure", _ => { calls++; return "success"; }));
        AssertFailure(result.Map(_ => { calls++; return 1; }));
        AssertFailure(result.Bind(_ => { calls++; return Result<int>.Ok(1); }));
        AssertFailure(result.SelectMany(_ => { calls++; return Result<int>.Ok(1); }, (_, _) => { calls++; return 1; }));
        AssertFailure(result.Tap(_ => calls++));
        AssertFailure(result.Where(_ => { calls++; return true; }));
        AssertFailure(result.OrElse(default(Result<T>)));
        AssertFailure(result.OrElse(_ => default));
        AssertFailure(await result.AsTask().Map(_ => { calls++; return 1; }));
        AssertFailure(await result.Bind(_ => { calls++; return Task.FromResult(Result<int>.Ok(1)); }));
        AssertFailure(await result.AsTask().SelectMany(
            _ => { calls++; return Task.FromResult(Result<int>.Ok(1)); },
            (_, _) => { calls++; return 1; }));
        AssertFailure(ResultStep<int, T>.FromResult(0, result).ToResult());
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("default", false)]
    [InlineData("new", false)]
    [InlineData("array", false)]
    [InlineData("default", true)]
    [InlineData("new", true)]
    [InlineData("array", true)]
    public async Task UninitializedStepNeverRunsSuccessContinuations(string source, bool referenceType)
    {
        if (referenceType)
            await CheckStep<string>(source);
        else
            await CheckStep<int>(source);
    }

    private static async Task CheckStep<T>(string source)
    {
        ResultStep<T, T> step = source switch
        {
            "new" => new ResultStep<T, T>(),
            "array" => (new ResultStep<T, T>[1])[0],
            _ => default
        };
        AssertFailure(step);
        Assert.False(step.IsSuspended);
        var calls = 0;
        Assert.Equal("failure", step.Match((_, _) => "failure", (_, _) => { calls++; return "success"; }));
        AssertFailure(step.Map(_ => { calls++; return 1; }));
        AssertFailure(step.MapState((state, _) => { calls++; return state; }));
        AssertFailure(step.Bind(_ => { calls++; return ResultStep<T, int>.Success(default!, 1); }));
        AssertFailure(step.SelectMany(
            _ => { calls++; return ResultStep<T, int>.Success(default!, 1); },
            (_, _) => { calls++; return 1; }));
        AssertFailure(step.Tap(_ => calls++));
        AssertFailure(step.Where(_ => { calls++; return true; }));
        AssertFailure(step.WithState(default!));
        AssertFailure(step.ToResult());
        AssertFailure(await step.AsTask().Map(_ => { calls++; return 1; }));
        AssertFailure(await step.BindAsync(_ => { calls++; return Task.FromResult(ResultStep<T, int>.Success(default!, 1)); }));
        AssertFailure(await step.AsTask().SelectMany(
            _ => { calls++; return Task.FromResult(ResultStep<T, int>.Success(default!, 1)); },
            (_, _) => { calls++; return 1; }));
        AssertFailure(PipelineStep.Loop(step, 3, (_, _) => { calls++; return step; }));
        AssertFailure(PipelineStep.LoopUntil(step, TimeSpan.FromSeconds(30), DateTimeOffset.UnixEpoch,
            () => { calls++; return DateTimeOffset.UnixEpoch; }, 3, (_, _, _) => { calls++; return step; }));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("empty_json")]
    [InlineData("empty_fields")]
    [InlineData("optional_fields")]
    public void MissingErrorInformationDoesNotTurnFailureIntoSuccess(string source)
    {
        ErrorContext? error = source switch
        {
            "empty_json" => JsonSerializer.Deserialize<ErrorContext>("{}"),
            "empty_fields" => new ErrorContext("", "", false),
            "optional_fields" => new ErrorContext("blocked", "BLOCKED", false),
            _ => default
        };
        var result = Result<int>.Fail(error!);
        var step = ResultStep<int, int>.Fail(0, error!);
        var calls = 0;
        AssertFailure(result);
        AssertFailure(step);
        Assert.Equal("failure", result.Match(_ => "failure", _ => { calls++; return "success"; }));
        Assert.Equal("failure", step.Match((_, _) => "failure", (_, _) => { calls++; return "success"; }));
        AssertFailure(result.Bind(_ => { calls++; return Result<int>.Ok(1); }));
        AssertFailure(step.Bind(_ => { calls++; return ResultStep<int, int>.Success(1, 1); }));
        AssertFailure(step.ToResult());
        AssertFailure(ResultStep<int, int>.FromResult(0, result));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UninitializedBinderOutputStopsSubsequentWork(bool asynchronous)
    {
        var calls = 0;
        var result = asynchronous
            ? await Result<int>.Ok(1).Bind(_ => Task.FromResult(default(Result<int>)))
            : Result<int>.Ok(1).Bind(_ => default(Result<int>));
        var step = asynchronous
            ? await ResultStep<int, int>.Success(1, 1).BindAsync(_ => Task.FromResult(default(ResultStep<int, int>)))
            : ResultStep<int, int>.Success(1, 1).Bind(_ => default(ResultStep<int, int>));
        AssertFailure(result.Tap(_ => calls++));
        AssertFailure(step.Tap(_ => calls++));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ExplicitSuccessWithZeroFalseOrEmptyValueIsStillSuccess()
    {
        Assert.True(Result<int>.Ok(0).IsSuccess);
        Assert.True(Result<bool>.Ok(false).IsSuccess);
        Assert.True(Result<string>.Ok("").IsSuccess);
        Assert.True(ResultStep<int, int>.Success(0, 0).IsSuccess);
    }

    private static void AssertFailure<T>(Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
    }

    private static void AssertFailure<TState, TValue>(ResultStep<TState, TValue> step)
    {
        Assert.False(step.IsSuccess);
        Assert.True(step.IsFailure);
    }
}
