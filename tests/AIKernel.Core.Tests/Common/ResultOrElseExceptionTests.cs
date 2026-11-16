namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class ResultOrElseExceptionTests
{
    public static TheoryData<string, bool> ExceptionCases
    {
        get
        {
            var cases = new TheoryData<string, bool>();
            foreach (var kind in new[] { "invalid_operation", "io", "timeout", "canceled", "task_canceled", "derived", "aggregate" })
                foreach (var referenceType in new[] { false, true })
                    cases.Add(kind, referenceType);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ExceptionCases))]
    public void ThrowingFallbackReturnsFailureAndPreservesSource(string kind, bool referenceType)
    {
        if (referenceType)
            CheckThrowingFallback<string>(kind);
        else
            CheckThrowingFallback<int>(kind);
    }

    private static void CheckThrowingFallback<T>(string kind)
    {
        var source = SourceError();
        var originalMetadata = source.Metadata!.ToArray();
        var exception = CreateException(kind);
        var calls = 0;
        var input = Result<T>.Fail(source);

        var result = input.OrElse(error =>
        {
            Assert.Same(source, error);
            calls++;
            throw exception;
        });

        var failure = AssertExceptionFailure(result, exception);
        AssertSource(failure, source);
        Assert.Equal(1, calls);
        Assert.Same(source, input.Error);
        Assert.NotNull(source.Metadata);
        Assert.Equal(originalMetadata, source.Metadata.ToArray());
        Assert.NotSame(source.Metadata, failure.Metadata);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSourceErrorStillProducesUnhandledException(bool explicitNull)
    {
        var input = explicitNull ? Result<int>.Fail((ErrorContext)null!) : default;
        var exception = new InvalidOperationException("fallback failed");
        var result = input.OrElse(error =>
        {
            Assert.Null(error);
            throw exception;
        });

        var failure = AssertExceptionFailure(result, exception);
        Assert.Equal(new KeyValuePair<string, string>("exception_type", exception.GetType().FullName!), Assert.Single(failure.Metadata!));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void MissingOptionalInformationIsNotInvented(string? message, string? code)
    {
        var source = new ErrorContext(message!, code!, false);
        var exception = new IOException("fallback failed");
        var result = Result<int>.Fail(source).OrElse(_ => throw exception);
        var failure = AssertExceptionFailure(result, exception);

        AssertSource(failure, source);
        Assert.False(failure.Metadata!.ContainsKey("source_error_failure_kind"));
        Assert.False(failure.Metadata.ContainsKey("source_error_origin_step"));
        Assert.False(failure.Metadata.ContainsKey("source_error_semantic_slot"));
    }

    [Fact]
    public void RepeatedFallbackFailuresKeepBothCausesWithoutKeyCollisions()
    {
        var source = SourceError();
        var firstException = new InvalidOperationException("first fallback");
        var secondException = new IOException("second fallback");
        var first = Result<int>.Fail(source).OrElse(_ => throw firstException);
        var second = first.OrElse(_ => throw secondException);

        var failure = AssertExceptionFailure(second, secondException);
        AssertSource(failure, first.Error!);
        Assert.Equal(source.Code, failure.Metadata!["source_error_metadata.source_error_code"]);
        Assert.Equal(source.Message, failure.Metadata["source_error_metadata.source_error_message"]);
        Assert.Equal("original-exception", failure.Metadata["source_error_metadata.source_error_metadata.exception_type"]);
        Assert.Equal("older-code", failure.Metadata["source_error_metadata.source_error_metadata.source_error_code"]);
    }

    [Fact]
    public void CapturedMetadataIsIndependentOfLaterSourceMutations()
    {
        var metadata = new Dictionary<string, string> { ["detail"] = "before" };
        var source = new ErrorContext("blocked", "BLOCKED", false) { Metadata = metadata };
        var exception = new IOException("fallback failed");
        var result = Result<int>.Fail(source).OrElse(_ => throw exception);

        metadata.Clear();
        metadata["detail"] = "after";

        var failure = AssertExceptionFailure(result, exception);
        Assert.Equal("before", failure.Metadata!["source_error_metadata.detail"]);
    }

    [Fact]
    public void SuccessDoesNotInvokeThrowingFallback()
    {
        var value = new object();
        var input = Result<object>.Ok(value);
        var calls = 0;
        var result = input.OrElse(_ => { calls++; throw new IOException("must not run"); });

        Assert.True(result.IsSuccess);
        Assert.Same(value, result.Value);
        Assert.Null(result.Error);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("default")]
    public void ReturnedFallbackResultRemainsUnchanged(string kind)
    {
        var source = SourceError();
        var expected = kind switch
        {
            "success" => Result<int>.Ok(42),
            "failure" => Result<int>.Fail(new ErrorContext("fallback rejected", "REJECTED", false)),
            _ => default
        };
        var calls = 0;
        var result = Result<int>.Fail(source).OrElse(error =>
        {
            Assert.Same(source, error);
            calls++;
            return expected;
        });

        Assert.Equal(expected, result);
        Assert.Same(expected.Error, result.Error);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValueOverloadKeepsExistingSelection(bool success)
    {
        var input = success ? Result<int>.Ok(1) : Result<int>.Fail(SourceError());
        var fallback = Result<int>.Fail(new ErrorContext("other", "OTHER", false));
        Assert.Equal(success ? input : fallback, input.OrElse(fallback));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullDelegateKeepsArgumentValidation(bool success)
    {
        var input = success ? Result<int>.Ok(1) : Result<int>.Fail(SourceError());
        var error = Assert.Throws<ArgumentNullException>(() => input.OrElse((Func<ErrorContext, Result<int>>)null!));
        Assert.Equal("fallback", error.ParamName);
    }

    [Fact]
    public void FailedFallbackDoesNotRunSubsequentSuccessWork()
    {
        var calls = 0;
        var result = Result<int>.Fail(SourceError())
            .OrElse(_ => throw new IOException("fallback failed"))
            .Map(value => { calls++; return value + 1; })
            .Bind(value => { calls++; return Result<int>.Ok(value); })
            .Tap(_ => calls++)
            .Where(_ => { calls++; return true; });

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void NonThrowingFallbackKeepsExistingStepAndReplayIdentity()
    {
        var expected = Result<int>.Fail(SourceError());
        var actual = Result<int>.Fail("initial failure").OrElse(_ => expected);
        var delta = new SemanticDelta("or_else", OriginStep.KernelFacade, SemanticSlot.T);
        var expectedStep = ResultStep<int, int>.FromResult(7, expected).WithSemanticDelta(delta);
        var actualStep = ResultStep<int, int>.FromResult(7, actual).WithSemanticDelta(delta);

        Assert.Equal(expectedStep.StepId, actualStep.StepId);
        Assert.Equal(expectedStep.ReplayLogHash, actualStep.ReplayLogHash);
        Assert.Equal(expectedStep.ReplayLog, actualStep.ReplayLog);
    }

    private static ErrorContext AssertExceptionFailure<T>(Result<T> result, Exception exception)
    {
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccessState);
        Assert.NotNull(result.Error);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
        Assert.Equal(exception.Message, result.Error.Message);
        Assert.False(result.Error.IsRetryable);
        Assert.Null(result.Error.FailureKind);
        Assert.Null(result.Error.OriginStep);
        Assert.Null(result.Error.SemanticSlot);
        Assert.NotNull(result.Error.Metadata);
        Assert.Equal(exception.GetType().FullName, result.Error.Metadata["exception_type"]);
        return result.Error;
    }

    private static void AssertSource(ErrorContext error, ErrorContext source)
    {
        Assert.NotNull(error.Metadata);
        AssertOptional("source_error_code", source.Code);
        AssertOptional("source_error_message", source.Message);
        AssertOptional("source_error_is_retryable", source.IsRetryable.ToString());
        AssertOptional("source_error_failure_kind", source.FailureKind?.ToString());
        AssertOptional("source_error_origin_step", source.OriginStep?.ToString());
        AssertOptional("source_error_semantic_slot", source.SemanticSlot?.ToString());
        foreach (var pair in source.Metadata ?? new Dictionary<string, string>())
            Assert.Equal(pair.Value, error.Metadata["source_error_metadata." + pair.Key]);

        void AssertOptional(string key, string? expected)
        {
            if (expected is null) Assert.False(error.Metadata.ContainsKey(key));
            else Assert.Equal(expected, error.Metadata[key]);
        }
    }

    private static ErrorContext SourceError() => new("source failure\n日本語 = details", "SOURCE_REJECTED", true)
    {
        FailureKind = FailureKind.Quarantine,
        OriginStep = OriginStep.Provider,
        SemanticSlot = SemanticSlot.T,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["exception_type"] = "original-exception",
            ["source_error_code"] = "older-code",
            ["source_error_message"] = "metadata-message",
            ["source_error_metadata.exception_type"] = "older-exception",
            ["detail"] = "line1\nline2=value",
            ["Case"] = "upper",
            ["case"] = "lower",
            [""] = "empty key"
        }
    };

    private static Exception CreateException(string kind) => kind switch
    {
        "io" => new IOException("fallback failed"),
        "timeout" => new TimeoutException("fallback failed"),
        "canceled" => new OperationCanceledException("fallback failed"),
        "task_canceled" => new TaskCanceledException("fallback failed"),
        "derived" => new DerivedFailure(),
        "aggregate" => new AggregateException(new IOException("fallback failed")),
        _ => new InvalidOperationException("fallback failed")
    };

    private sealed class DerivedFailure() : Exception("fallback failed");
}
