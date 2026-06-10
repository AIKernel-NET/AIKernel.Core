namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class ResultTests
{
    [Fact]
    public void OkAliasCreatesSuccessfulResult()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccessState);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void OrElseUsesFallbackOnlyForFailure()
    {
        var success = Result<int>.Success(1).OrElse(Result<int>.Success(2));
        var failure = Result<int>.Fail("blocked").OrElse(_ => Result<int>.Success(3));

        Assert.Equal(1, success.Value);
        Assert.Equal(3, failure.Value);
    }

    [Fact]
    public void Map_TransformsSuccess()
    {
        var result = Result<int>.Success(2).Map(x => x + 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void ErrorContext_OptionalFields_AreNullByDefault()
    {
        var error = new ErrorContext("blocked", "BLOCKED", false);

        Assert.Null(error.FailureKind);
        Assert.Null(error.OriginStep);
        Assert.Null(error.SemanticSlot);
        Assert.Null(error.Metadata);
    }

    [Fact]
    public void ReplayMetadataKeys_ExposeStableContractNames()
    {
        Assert.Equal("step_id", ReplayMetadataKeys.StepId);
        Assert.Equal("semantic_delta", ReplayMetadataKeys.SemanticDelta);
        Assert.Equal("replay_log_count", ReplayMetadataKeys.ReplayLogCount);
        Assert.Equal("replay_log_hash", ReplayMetadataKeys.ReplayLogHash);
        Assert.Equal("origin_step", ReplayMetadataKeys.OriginStep);
        Assert.Equal("semantic_slot", ReplayMetadataKeys.SemanticSlot);
        Assert.Equal("failure_kind", ReplayMetadataKeys.FailureKind);
    }

    [Fact]
    public void ResultMetadataKeys_ExposeStableContractNames()
    {
        Assert.Equal("exception_type", ResultMetadataKeys.ExceptionType);
        Assert.Equal("source_error_code", ResultMetadataKeys.SourceErrorCode);
    }

    [Fact]
    public void ErrorContext_FromException_AddsExceptionTypeMetadata()
    {
        var error = ErrorContext.FromException(
            new InvalidOperationException("boom"));

        Assert.Equal("boom", error.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", error.Code);
        Assert.False(error.IsRetryable);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            error.Metadata![ResultMetadataKeys.ExceptionType]);
    }

    [Fact]
    public void ExecutionMetadataKeys_ExposeStableContractNames()
    {
        Assert.Equal("message_format", ExecutionMetadataKeys.MessageFormat);
        Assert.Equal("overflow_policy", ExecutionMetadataKeys.OverflowPolicy);
        Assert.Equal("dsl_rom_hash", ExecutionMetadataKeys.DslRomHash);
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
    public void Tap_RunsActionForSuccessAndPreservesValue()
    {
        var observed = 0;

        var result = Result<int>
            .Success(4)
            .Tap(value => observed = value);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value);
        Assert.Equal(4, observed);
    }

    [Fact]
    public void Tap_ShortCircuitsFailureWithoutRunningAction()
    {
        var called = false;
        var failure = new ErrorContext("blocked", "BLOCKED", false);

        var result = Result<int>
            .Fail(failure)
            .Tap(_ => called = true);

        Assert.True(result.IsFailure);
        Assert.False(called);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public void Tap_CatchesActionException()
    {
        var result = Result<int>
            .Success(4)
            .Tap(_ => throw new InvalidOperationException("tap-boom"));

        Assert.True(result.IsFailure);
        Assert.Equal("tap-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
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
        Assert.Equal("PREDICATE_FAILED", result.Error.Code);
    }

    [Fact]
    public void Where_CatchesPredicateException()
    {
        var result =
            from value in Result<int>.Success(1)
            where Throws(value)
            select value;

        Assert.True(result.IsFailure);
        Assert.Equal("predicate-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    private static bool Throws(int _)
    {
        throw new InvalidOperationException("predicate-boom");
    }
}
